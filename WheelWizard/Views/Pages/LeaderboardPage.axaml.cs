using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using WheelWizard.Models;
using WheelWizard.RrRooms;
using WheelWizard.Services.LiveData;
using WheelWizard.Settings;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Shared.Services;
using WheelWizard.Utilities.Generators;
using WheelWizard.Views.Popups;
using WheelWizard.Views.Popups.Generic;
using WheelWizard.Views.Popups.MiiManagement;
using WheelWizard.WheelWizardData;
using WheelWizard.WheelWizardData.Domain;
using WheelWizard.WiiManagement.GameLicense;
using WheelWizard.WiiManagement.MiiManagement;
using WheelWizard.WiiManagement.MiiManagement.Domain.Mii;

namespace WheelWizard.Views.Pages;

public sealed record LeaderboardPlayerItem
{
    public required int Rank { get; init; }
    public required string PlacementLabel { get; init; }
    public required string Name { get; init; }
    public required string FriendCode { get; init; }
    public required string VrText { get; init; }
    public Mii? Mii { get; init; }
    public BadgeVariant PrimaryBadge { get; init; }
    public bool HasBadge { get; init; }
    public bool IsSuspicious { get; init; }
    public bool IsEvenRow { get; init; }
    public bool IsFriend { get; init; }
    public bool IsOnline { get; init; }

    // Keep parity with RoomDetailsPage player template bindings.
    public string VrDisplay => VrText;
    public Mii? FirstMii => Mii;
    public bool HasBadges => HasBadge;
    public bool IsTopLeaderboardPlayer => true;
    public string TopLabel => $"#{Rank}";
    public bool IsOpenHost => false;
}

public sealed record LeaderboardSearchMatch(int Rank, string Name, string FriendCode, string VrText, Mii? Mii, bool IsSuspicious);

public partial class LeaderboardPage : UserControlBase, INotifyPropertyChanged
{
    private static readonly LeaderboardPlayerItem EmptyPodiumPlayer = new()
    {
        Rank = 0,
        PlacementLabel = string.Empty,
        Name = string.Empty,
        FriendCode = string.Empty,
        VrText = string.Empty,
        PrimaryBadge = BadgeVariant.None,
        HasBadge = false,
        IsSuspicious = false,
        IsEvenRow = true,
    };

    private CancellationTokenSource? _loadCts;

    [Inject]
    private IRrLeaderboardSingletonService LeaderboardService { get; set; } = null!;

    [Inject]
    private IWhWzDataSingletonService BadgeService { get; set; } = null!;

    [Inject]
    private IGameLicenseSingletonService GameDataService { get; set; } = null!;

    [Inject]
    private ISettingsManager SettingsManager { get; set; } = null!;

    [Inject]
    private IApiCaller<IRwfcApi> ApiCaller { get; set; } = null!;

    [Inject]
    private IMiiDbService MiiDbService { get; set; } = null!;

    private bool _hasLoadedOnce;
    private bool _isLoading;
    private bool _hasError;
    private bool _hasNoData;
    private bool _hasData;
    private string _errorMessage = string.Empty;
    private LeaderboardPlayerItem? _podiumFirst;
    private LeaderboardPlayerItem? _podiumSecond;
    private LeaderboardPlayerItem? _podiumThird;
    private readonly List<LeaderboardSearchMatch> _searchMatches = [];
    private int _selectedSearchMatchIndex = -1;
    private int _currentLeaderboardPage = 1;
    private int _totalLeaderboardPages = 1;

    public ObservableCollection<LeaderboardPlayerItem> RemainingPlayers { get; } = [];

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (_hasError == value)
                return;
            _hasError = value;
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
                return;
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public bool HasNoData
    {
        get => _hasNoData;
        private set
        {
            if (_hasNoData == value)
                return;
            _hasNoData = value;
            OnPropertyChanged(nameof(HasNoData));
        }
    }

    public bool HasData
    {
        get => _hasData;
        private set
        {
            if (_hasData == value)
                return;
            _hasData = value;
            OnPropertyChanged(nameof(HasData));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
                return;
            _errorMessage = value;
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }

    public string RemainingCountText => $"{RemainingPlayers.Count} players";
    public string LeaderboardPageText => $"Page {_currentLeaderboardPage} / {_totalLeaderboardPages}";
    public bool CanGoToPreviousPage => _currentLeaderboardPage > 1 && !IsLoading;
    public bool CanGoToNextPage => _currentLeaderboardPage < _totalLeaderboardPages && !IsLoading;
    public bool HasActiveSearchResult => _selectedSearchMatchIndex >= 0 && _selectedSearchMatchIndex < _searchMatches.Count;
    public string PodiumTitleText =>
        HasActiveSearchResult ? "Player"
        : _currentLeaderboardPage == 1 ? "Top 3 Podium"
        : "Page leaders";
    public string PodiumCaptionText =>
        HasActiveSearchResult ? string.Empty
        : _currentLeaderboardPage == 1 ? "Live from RWFC"
        : LeaderboardPageText;

    public LeaderboardPlayerItem? PodiumFirst
    {
        get => _podiumFirst ?? EmptyPodiumPlayer;
        private set => SetPodiumPlayer(ref _podiumFirst, value, nameof(PodiumFirst), nameof(HasPodiumFirst));
    }

    public LeaderboardPlayerItem? PodiumSecond
    {
        get => _podiumSecond ?? EmptyPodiumPlayer;
        private set => SetPodiumPlayer(ref _podiumSecond, value, nameof(PodiumSecond), nameof(HasPodiumSecond));
    }

    public LeaderboardPlayerItem? PodiumThird
    {
        get => _podiumThird ?? EmptyPodiumPlayer;
        private set => SetPodiumPlayer(ref _podiumThird, value, nameof(PodiumThird), nameof(HasPodiumThird));
    }

    public bool HasPodiumFirst => _podiumFirst != null;
    public bool HasPodiumSecond => _podiumSecond != null;
    public bool HasPodiumThird => _podiumThird != null;

    public LeaderboardPage()
    {
        InitializeComponent();
        DataContext = this;
        RemainingPlayers.CollectionChanged += RemainingPlayers_OnCollectionChanged;

        Loaded += LeaderboardPage_Loaded;
        Unloaded += LeaderboardPage_Unloaded;
    }

    private async void LeaderboardPage_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_hasLoadedOnce)
            return;

        _hasLoadedOnce = true;
        await ReloadLeaderboardAsync();
    }

    private void LeaderboardPage_Unloaded(object? sender, RoutedEventArgs e)
    {
        CancelCurrentLoad();
    }

    private void RemainingPlayers_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RemainingCountText));
    }

    private void LeaderboardSearchField_OnTextChanged(object? sender, TextChangedEventArgs e) =>
        LeaderboardSearchStatus.Text = string.Empty;

    private void LeaderboardSearchField_OnKeyDown(object? sender, KeyEventArgs e) { }

    private async void SearchLeaderboardPlayer_OnClick(object? sender, RoutedEventArgs e)
    {
        var input = await new TextInputWindow()
            .SetMainText("Search leaderboard")
            .SetExtraText("Search by player name or 12-digit friend code.")
            .SetPlaceholderText("Player or 0000-0000-0000")
            .SetButtonText("Cancel", "Search")
            .ShowDialog();

        if (input == null)
            return;

        await SearchLeaderboardPlayer(input);
    }

    private async Task SearchLeaderboardPlayer(string? requestedQuery = null)
    {
        var query = requestedQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearLeaderboardSearchResult();
            if (_currentLeaderboardPage != 1 || !HasData)
                await ReloadLeaderboardAsync(1);
            return;
        }

        var normalizedFriendCode = TryNormalizeFriendCode(query);
        LeaderboardSearchButton.IsEnabled = false;

        SetLeaderboardSearchStatus(
            normalizedFriendCode != null ? $"Looking up {normalizedFriendCode}…" : $"Searching RWFC for \"{query}\"…",
            false
        );

        try
        {
            if (normalizedFriendCode != null)
            {
                var profileResult = await ApiCaller.CallApiAsync(api => api.GetPlayerProfileAsync(normalizedFriendCode));
                if (profileResult.IsFailure || profileResult.Value == null)
                {
                    SetLeaderboardSearchStatus($"No RWFC profile was found for {normalizedFriendCode}.", true);
                    return;
                }

                var rank = profileResult.Value.Rank > 0 ? profileResult.Value.Rank : 0;
                SetLeaderboardSearchMatches(
                    [
                        new LeaderboardSearchMatch(
                            rank,
                            string.IsNullOrWhiteSpace(profileResult.Value.Name) ? "Unknown Player" : profileResult.Value.Name,
                            normalizedFriendCode,
                            profileResult.Value.Vr.ToString("N0"),
                            DeserializeMii(profileResult.Value.MiiData),
                            profileResult.Value.IsSuspicious
                        ),
                    ],
                    1
                );
                return;
            }

            var searchResult = await ApiCaller.CallApiAsync(api => api.SearchLeaderboardAsync(query));
            if (searchResult.IsFailure || searchResult.Value == null)
            {
                SetLeaderboardSearchStatus("RWFC leaderboard search failed. Try again in a moment.", true);
                return;
            }

            var matches = searchResult
                .Value.Players.Select((entry, index) => new { Entry = entry, Rank = ResolveRank(entry, index) })
                .Where(item => !string.IsNullOrWhiteSpace(item.Entry.FriendCode))
                .Select(item => new LeaderboardSearchMatch(
                    item.Rank,
                    string.IsNullOrWhiteSpace(item.Entry.Name) ? "Unknown Player" : item.Entry.Name,
                    item.Entry.FriendCode,
                    item.Entry.Vr?.ToString("N0") ?? "--",
                    DeserializeMii(item.Entry.MiiData),
                    item.Entry.IsSuspicious
                ))
                .ToList();

            if (matches.Count == 0)
            {
                SetLeaderboardSearchStatus($"No leaderboard players matched \"{query}\".", true);
                return;
            }

            SetLeaderboardSearchMatches(matches, searchResult.Value.TotalCount);
        }
        finally
        {
            LeaderboardSearchButton.IsEnabled = true;
        }
    }

    private async void RetryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ReloadLeaderboardAsync();
    }

    private async void PreviousLeaderboardPage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentLeaderboardPage <= 1)
            return;

        ClearLeaderboardSearchResult();
        await ReloadLeaderboardAsync(_currentLeaderboardPage - 1);
    }

    private async void NextLeaderboardPage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentLeaderboardPage >= _totalLeaderboardPages)
            return;

        ClearLeaderboardSearchResult();
        await ReloadLeaderboardAsync(_currentLeaderboardPage + 1);
    }

    private int _leaderboardSearchTotalMatches;

    private void SetLeaderboardSearchMatches(List<LeaderboardSearchMatch> matches, int totalMatches)
    {
        _searchMatches.Clear();
        _searchMatches.AddRange(matches);
        _leaderboardSearchTotalMatches = totalMatches > 0 ? totalMatches : matches.Count;
        _selectedSearchMatchIndex = matches.Count > 0 ? 0 : -1;
        ApplyCurrentLeaderboardSearchMatch();
    }

    private void ClearLeaderboardSearchResult()
    {
        _searchMatches.Clear();
        _selectedSearchMatchIndex = -1;
        _leaderboardSearchTotalMatches = 0;
        LeaderboardSearchStatus.Text = string.Empty;
        ApplyCurrentLeaderboardSearchMatch();
        OnPropertyChanged(nameof(HasActiveSearchResult));
        OnPropertyChanged(nameof(PodiumTitleText));
        OnPropertyChanged(nameof(PodiumCaptionText));
    }

    private void ApplyCurrentLeaderboardSearchMatch()
    {
        if (_selectedSearchMatchIndex < 0 || _selectedSearchMatchIndex >= _searchMatches.Count)
        {
            LeaderboardSearchResultCard.IsVisible = false;
            DefaultPodiumFirstCard.IsVisible = HasPodiumFirst;
            DefaultPodiumSecondCard.IsVisible = HasPodiumSecond;
            DefaultPodiumThirdCard.IsVisible = HasPodiumThird;
            SearchResultPodiumControls.IsVisible = false;
            SearchResultSingleLayout.IsVisible = false;
            CloseSearchResultButton.IsVisible = false;
            LeaderboardSearchOpenProfileButton.IsVisible = false;
            PreviousSearchMatchButton.IsEnabled = false;
            NextSearchMatchButton.IsEnabled = false;
            LeaderboardSearchOpenProfileButton.IsEnabled = false;
            LeaderboardSearchMatchIndex.Text = string.Empty;
            SearchResultCompactCard.IsVisible = false;
            SearchResultRankText.Text = string.Empty;
            SearchResultPlayerName.Text = string.Empty;
            SearchResultVrText.Text = string.Empty;
            SearchResultMiiImage.Mii = null;
            OnPropertyChanged(nameof(HasActiveSearchResult));
            OnPropertyChanged(nameof(PodiumTitleText));
            OnPropertyChanged(nameof(PodiumCaptionText));
            return;
        }

        var match = _searchMatches[_selectedSearchMatchIndex];
        LeaderboardSearchResultCard.IsVisible = false;
        DefaultPodiumFirstCard.IsVisible = false;
        DefaultPodiumSecondCard.IsVisible = false;
        DefaultPodiumThirdCard.IsVisible = false;
        SearchResultPodiumControls.IsVisible = true;
        SearchResultSingleLayout.IsVisible = true;
        CloseSearchResultButton.IsVisible = true;
        SearchResultCompactCard.IsVisible = true;
        SearchResultRankText.Text = match.Rank > 0 ? $"#{match.Rank}" : "Search hit";
        SearchResultPlayerName.Text = match.Name;
        SearchResultVrText.Text = match.VrText;
        SearchResultMiiImage.Mii = match.Mii;
        LeaderboardSearchOpenProfileButton.IsEnabled = !string.IsNullOrWhiteSpace(match.FriendCode);
        LeaderboardSearchOpenProfileButton.IsVisible = LeaderboardSearchOpenProfileButton.IsEnabled;
        PreviousSearchMatchButton.IsEnabled = _searchMatches.Count > 1;
        NextSearchMatchButton.IsEnabled = _searchMatches.Count > 1;
        LeaderboardSearchMatchIndex.Text = string.Empty;

        OnPropertyChanged(nameof(HasActiveSearchResult));
        OnPropertyChanged(nameof(PodiumTitleText));
        OnPropertyChanged(nameof(PodiumCaptionText));
    }

    private void SetLeaderboardSearchStatus(string message, bool isError)
    {
        LeaderboardSearchResultCard.IsVisible = !string.IsNullOrWhiteSpace(message) && !HasActiveSearchResult;
        LeaderboardSearchStatus.Text = message;
        LeaderboardSearchStatus.Foreground = new Avalonia.Media.SolidColorBrush(
            isError ? ViewUtils.Colors.Danger400 : ViewUtils.Colors.Neutral400
        );
        OnPropertyChanged(nameof(PodiumCaptionText));
    }

    private void PreviousSearchMatch_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_searchMatches.Count <= 1)
            return;

        _selectedSearchMatchIndex = (_selectedSearchMatchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        ApplyCurrentLeaderboardSearchMatch();
    }

    private void NextSearchMatch_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_searchMatches.Count <= 1)
            return;

        _selectedSearchMatchIndex = (_selectedSearchMatchIndex + 1) % _searchMatches.Count;
        ApplyCurrentLeaderboardSearchMatch();
    }

    private void OpenSearchProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedSearchMatchIndex < 0 || _selectedSearchMatchIndex >= _searchMatches.Count)
            return;

        var match = _searchMatches[_selectedSearchMatchIndex];
        if (string.IsNullOrWhiteSpace(match.FriendCode))
            return;

        new PlayerProfileWindow(match.FriendCode).Show();
    }

    private void CloseSearchResult_OnClick(object? sender, RoutedEventArgs e)
    {
        ClearLeaderboardSearchResult();
    }

    private async Task ReloadLeaderboardAsync(int page = 1)
    {
        CancelCurrentLoad();
        _loadCts = new();
        var cancellationToken = _loadCts.Token;

        SetLoadingState();
        ClearLeaderboardData();
        await Task.Yield();

        var leaderboardResult = await ApiCaller.CallApiAsync(api => api.GetLeaderboardPageAsync(page));
        if (cancellationToken.IsCancellationRequested)
            return;

        if (leaderboardResult.IsFailure)
        {
            SetErrorState(leaderboardResult.Error?.Message ?? "Unable to fetch leaderboard.");
            return;
        }

        var leaderboardPage = leaderboardResult.Value;
        if (leaderboardPage == null)
        {
            SetErrorState("Unable to fetch leaderboard.");
            return;
        }

        _currentLeaderboardPage = leaderboardPage.CurrentPage > 0 ? leaderboardPage.CurrentPage : page;
        _totalLeaderboardPages = leaderboardPage.TotalPages > 0 ? leaderboardPage.TotalPages : 1;
        OnPropertyChanged(nameof(LeaderboardPageText));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        OnPropertyChanged(nameof(PodiumTitleText));
        OnPropertyChanged(nameof(PodiumCaptionText));

        var orderedEntries = leaderboardPage
            .Players.Select((entry, index) => new { Entry = entry, Rank = ResolveRank(entry, index) })
            .OrderBy(entry => entry.Rank)
            .ToList();

        var friendProfileIds = GameDataService
            .ActiveCurrentFriends.Select(friend => FriendCodeGenerator.FriendCodeToProfileId(friend.FriendCode))
            .Where(profileId => profileId != 0)
            .ToHashSet();
        var onlineProfileIds = RRLiveRooms
            .Instance.CurrentRooms.SelectMany(room => room.Players)
            .Select(player => FriendCodeGenerator.FriendCodeToProfileId(player.FriendCode))
            .Where(profileId => profileId != 0)
            .ToHashSet();

        if (orderedEntries.Count == 0)
        {
            SetEmptyState();
            return;
        }

        List<LeaderboardPlayerItem> mappedPlayers;
        try
        {
            mappedPlayers = await Task.Run(
                () =>
                    orderedEntries
                        .Select(
                            (entry, index) => CreateLeaderboardPlayer(entry.Entry, entry.Rank, index, friendProfileIds, onlineProfileIds)
                        )
                        .ToList(),
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        PodiumFirst = mappedPlayers.ElementAtOrDefault(0);
        PodiumSecond = mappedPlayers.ElementAtOrDefault(1);
        PodiumThird = mappedPlayers.ElementAtOrDefault(2);

        if (cancellationToken.IsCancellationRequested)
            return;

        foreach (var player in mappedPlayers.Skip(3))
        {
            RemainingPlayers.Add(player);
        }

        SetDataState();
    }

    private LeaderboardPlayerItem CreateLeaderboardPlayer(
        RwfcLeaderboardEntry entry,
        int rank,
        int index,
        IReadOnlySet<uint> friendProfileIds,
        IReadOnlySet<uint> onlineProfileIds
    )
    {
        var friendCode = entry.FriendCode ?? string.Empty;
        var profileId = FriendCodeGenerator.FriendCodeToProfileId(friendCode);
        var badges = string.IsNullOrWhiteSpace(friendCode) ? [] : BadgeService.GetBadges(friendCode);
        var primaryBadge = badges.FirstOrDefault(BadgeVariant.None);

        return new()
        {
            Rank = rank,
            PlacementLabel = GetPlacementLabel(rank),
            Name = string.IsNullOrWhiteSpace(entry.Name) ? "Unknown Player" : entry.Name,
            FriendCode = friendCode,
            VrText = entry.Vr?.ToString("N0") ?? "--",
            Mii = DeserializeMii(entry.MiiData),
            PrimaryBadge = primaryBadge,
            HasBadge = primaryBadge != BadgeVariant.None,
            IsSuspicious = entry.IsSuspicious,
            IsEvenRow = index % 2 == 0,
            IsFriend = profileId != 0 && friendProfileIds.Contains(profileId),
            IsOnline = profileId != 0 && onlineProfileIds.Contains(profileId),
        };
    }

    internal static int ResolveRank(RwfcLeaderboardEntry entry, int index)
    {
        if (entry.Rank is > 0)
            return entry.Rank.Value;

        if (entry.ActiveRank is > 0)
            return entry.ActiveRank.Value;

        return index + 1;
    }

    private string GetPlacementLabel(int rank)
    {
        if (_currentLeaderboardPage == 1)
        {
            return rank switch
            {
                1 => "Champion",
                2 => "2nd Place",
                3 => "3rd Place",
                _ => $"#{rank}",
            };
        }

        return $"{rank}{GetOrdinalSuffix(rank)} Place";
    }

    internal static string GetOrdinalSuffix(int value)
    {
        var lastTwoDigits = value % 100;
        if (lastTwoDigits is >= 11 and <= 13)
            return "th";

        return (value % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        };
    }

    private static Mii? DeserializeMii(string? miiData)
    {
        if (string.IsNullOrWhiteSpace(miiData))
            return null;

        // Mii block payload should be ~100 base64 chars (74 bytes decoded).
        // Guarding the size avoids expensive decode failures for large non-Mii payloads.
        if (miiData.Length is < 90 or > 120)
            return null;

        var buffer = new byte[MiiSerializer.MiiBlockSize];
        if (!Convert.TryFromBase64String(miiData, buffer, out var bytesWritten) || bytesWritten != MiiSerializer.MiiBlockSize)
            return null;

        var result = MiiSerializer.Deserialize(buffer);
        return result.IsSuccess ? result.Value : null;
    }

    private void SetPodiumPlayer(
        ref LeaderboardPlayerItem? field,
        LeaderboardPlayerItem? value,
        string propertyName,
        string hasPropertyName
    )
    {
        if (field == value)
            return;

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(hasPropertyName);
    }

    private void SetLoadingState()
    {
        IsLoading = true;
        HasError = false;
        HasNoData = false;
        HasData = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    private void SetErrorState(string message)
    {
        IsLoading = false;
        HasError = true;
        HasNoData = false;
        HasData = false;
        ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Failed to load leaderboard." : message;
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    private void SetEmptyState()
    {
        IsLoading = false;
        HasError = false;
        HasNoData = true;
        HasData = false;
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    private void SetDataState()
    {
        IsLoading = false;
        HasError = false;
        HasNoData = false;
        HasData = true;
        if (HasActiveSearchResult)
        {
            DefaultPodiumFirstCard.IsVisible = false;
            DefaultPodiumSecondCard.IsVisible = false;
            DefaultPodiumThirdCard.IsVisible = false;
            SearchResultSingleLayout.IsVisible = true;
            SearchResultCompactCard.IsVisible = true;
            SearchResultPodiumControls.IsVisible = true;
        }
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    private void ClearLeaderboardData()
    {
        PodiumFirst = null;
        PodiumSecond = null;
        PodiumThird = null;
        DefaultPodiumFirstCard.IsVisible = false;
        DefaultPodiumSecondCard.IsVisible = false;
        DefaultPodiumThirdCard.IsVisible = false;
        RemainingPlayers.Clear();
        OnPropertyChanged(nameof(RemainingCountText));
    }

    private void CancelCurrentLoad()
    {
        if (_loadCts == null)
            return;

        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = null;
    }

    private void CopyFriendCode_OnClick(object sender, RoutedEventArgs e)
    {
        var player = GetContextPlayer(sender);
        if (player == null || string.IsNullOrWhiteSpace(player.FriendCode))
            return;

        TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(player.FriendCode);
        ViewUtils.ShowSnackbar(t("snackbar_success.copied_fc"));
    }

    private void OpenCarousel_OnClick(object sender, RoutedEventArgs e)
    {
        var player = GetContextPlayer(sender);
        if (player?.FirstMii == null)
            return;

        new MiiCarouselWindow().SetMii(player.FirstMii).Show();
    }

    private void ViewProfile_OnClick(object sender, RoutedEventArgs e)
    {
        var player = GetContextPlayer(sender);
        if (player == null || string.IsNullOrWhiteSpace(player.FriendCode))
            return;

        new PlayerProfileWindow(player.FriendCode).Show();
    }

    private async void AddFriend_OnClick(object sender, RoutedEventArgs e)
    {
        var player = GetContextPlayer(sender);
        if (player == null)
            return;

        if (player.FirstMii == null)
        {
            ViewUtils.ShowSnackbar("This player has no valid Mii data.", ViewUtils.SnackbarType.Warning);
            return;
        }

        var focusedUserIndex = SettingsManager.Get<int>(SettingsManager.FOCUSED_USER);
        if (focusedUserIndex is < 0 or > 3)
        {
            ViewUtils.ShowSnackbar("Invalid license selected.", ViewUtils.SnackbarType.Warning);
            return;
        }

        var activeUserPid = FriendCodeGenerator.FriendCodeToProfileId(GameDataService.ActiveUser.FriendCode);
        if (activeUserPid == 0)
        {
            ViewUtils.ShowSnackbar("Select a valid license before adding friends.", ViewUtils.SnackbarType.Warning);
            return;
        }

        if (GameDataService.ActiveCurrentFriends.Count >= 30)
        {
            ViewUtils.ShowSnackbar("Your friend list is full.", ViewUtils.SnackbarType.Warning);
            return;
        }

        var normalizedFriendCodeResult = NormalizeFriendCode(player.FriendCode);
        if (normalizedFriendCodeResult.IsFailure)
        {
            ViewUtils.ShowSnackbar(normalizedFriendCodeResult.Error.Message, ViewUtils.SnackbarType.Warning);
            return;
        }

        var normalizedFriendCode = normalizedFriendCodeResult.Value;
        var friendProfileId = FriendCodeGenerator.FriendCodeToProfileId(normalizedFriendCode);
        if (activeUserPid == friendProfileId)
        {
            ViewUtils.ShowSnackbar("You cannot add your own friend code.", ViewUtils.SnackbarType.Warning);
            return;
        }

        var duplicateFriend = GameDataService.ActiveCurrentFriends.Any(friend =>
        {
            var existingPid = FriendCodeGenerator.FriendCodeToProfileId(friend.FriendCode);
            return existingPid != 0 && existingPid == friendProfileId;
        });

        if (duplicateFriend)
        {
            ViewUtils.ShowSnackbar("This friend is already in your list.", ViewUtils.SnackbarType.Warning);
            return;
        }

        var profile = new PlayerProfileResponse
        {
            Name = player.Name,
            FriendCode = normalizedFriendCode,
            Vr = int.TryParse(player.VrText.Replace(",", string.Empty), out var vr) ? vr : 0,
        };

        var shouldAdd = await new AddFriendConfirmationWindow(profile, player.FirstMii).AwaitAnswer();
        if (!shouldAdd)
            return;

        var addResult = GameDataService.AddFriend(focusedUserIndex, normalizedFriendCode, player.FirstMii, (uint)Math.Max(profile.Vr, 0));

        if (addResult.IsFailure)
        {
            ViewUtils.ShowSnackbar(addResult.Error.Message, ViewUtils.SnackbarType.Warning);
            return;
        }

        ViewUtils.GetLayout().UpdateFriendCount();
        ViewUtils.ShowSnackbar($"Added {player.Name} to your friend list.");
    }

    private void CopyMii_OnClick(object sender, RoutedEventArgs e)
    {
        var player = GetContextPlayer(sender);
        if (player is not { FirstMii: not null })
        {
            ViewUtils.ShowSnackbar("This player has no valid Mii data.", ViewUtils.SnackbarType.Warning);
            return;
        }

        var serialized = MiiSerializer.Serialize(player.FirstMii);
        if (serialized.IsFailure)
        {
            ViewUtils.ShowSnackbar(serialized.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        var copy = MiiSerializer.Deserialize(serialized.Value);
        if (copy.IsFailure)
        {
            ViewUtils.ShowSnackbar(copy.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        var save = MiiDbService.AddToDatabase(copy.Value, "02:11:11:11:11:11");
        if (save.IsFailure)
        {
            ViewUtils.ShowSnackbar(save.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        ViewUtils.ShowSnackbar($"Copied {player.Name}'s Mii to My Miis.");
    }

    private void ViewPlayerOnRwfc_OnClick(object sender, RoutedEventArgs e)
    {
        var player = GetContextPlayer(sender);
        if (player == null)
            return;

        var normalizedFriendCode = NormalizeFriendCode(player.FriendCode);
        if (normalizedFriendCode.IsFailure)
        {
            ViewUtils.ShowSnackbar("This player has no valid RWFC profile link.", ViewUtils.SnackbarType.Warning);
            return;
        }

        ViewUtils.OpenLink($"https://rwfc.net/player/{Uri.EscapeDataString(normalizedFriendCode.Value)}");
    }

    private void JoinRoom_OnClick(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return;

        foreach (var room in RRLiveRooms.Instance.CurrentRooms)
        {
            if (room.Players.All(player => player.FriendCode != friendCode))
                continue;

            NavigationManager.NavigateTo<RoomDetailsPage>(room);
            return;
        }

        ViewUtils.ShowSnackbar("Could not find an active room for this player.", ViewUtils.SnackbarType.Warning);
    }

    private static LeaderboardPlayerItem? GetContextPlayer(object sender)
    {
        if (sender is not Control control)
            return null;
        return control.DataContext as LeaderboardPlayerItem;
    }

    private static OperationResult<string> NormalizeFriendCode(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return Fail("Friend code cannot be empty.");

        var digits = new string(friendCode.Where(char.IsDigit).ToArray());
        if (digits.Length != 12 || !ulong.TryParse(digits, out _))
            return Fail("Friend code must be exactly 12 digits.");

        var formatted = $"{digits[..4]}-{digits.Substring(4, 4)}-{digits.Substring(8, 4)}";
        var profileId = FriendCodeGenerator.FriendCodeToProfileId(formatted);
        if (profileId == 0)
            return Fail("Invalid friend code.");

        return formatted;
    }

    private static string? TryNormalizeFriendCode(string? friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return null;

        var digits = new string(friendCode.Where(char.IsDigit).ToArray());
        return digits.Length == 12 ? $"{digits[..4]}-{digits.Substring(4, 4)}-{digits.Substring(8, 4)}" : null;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}
