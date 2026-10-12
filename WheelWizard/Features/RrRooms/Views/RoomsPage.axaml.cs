using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WheelWizard.Models.RRInfo;
using WheelWizard.RrRooms;
using WheelWizard.Shared.Polling;
using WheelWizard.Views.Shell.Navigation;
using WheelWizard.WheelWizardData;

namespace WheelWizard.RrRooms.Views;

public partial class RoomsPage : UserControl, INotifyPropertyChanged, IPollingListener
{
    private INavigationService Navigation { get; }

    private LiveRoomsService LiveRooms { get; }

    private string? _searchQuery;
    private int? _minimumAverageVr;
    private int? _maximumAverageVr;
    private int _averageVrSort;
    private readonly DispatcherTimer _raceTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private readonly ObservableCollection<RrRoom> _rooms = [];

    public ObservableCollection<RrRoom> Rooms
    {
        get => _rooms;
        init
        {
            _rooms = value;
            OnPropertyChanged(nameof(Rooms));
        }
    }

    private readonly ObservableCollection<RrPlayer> _players = [];

    public ObservableCollection<RrPlayer> Players
    {
        get => _players;
        init
        {
            _players = value;
            OnPropertyChanged(nameof(Players));
        }
    }

    private IPopupFactory Popups { get; }

    public RoomsPage(INavigationService navigation, LiveRoomsService liveRooms, IPopupFactory popups)
    {
        Navigation = navigation;
        LiveRooms = liveRooms;
        Popups = popups;
        InitializeComponent();
        DataContext = this;
        LiveRooms.Subscribe(this);
        _raceTimer.Tick += RaceTimer_OnTick;
        _raceTimer.Start();

        OnUpdate(LiveRooms);
        Unloaded += RoomsPage_Unloaded;
    }

    public void OnUpdate(ObservablePollingService sender)
    {
        if (sender is not LiveRoomsService liveRooms)
            return;

        var count = liveRooms.RoomCount;
        EmptyRoomsView.IsVisible = count == 0;
        RoomsListViewContainer.IsVisible = count != 0;
        if (count == 0)
            return;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = _searchQuery?.Trim();
        IEnumerable<RrRoom> filteredRooms = LiveRooms
            .CurrentRooms.Where(room => room.PlayerCount > 0)
            .Where(room => !_minimumAverageVr.HasValue || room.AverageVr >= _minimumAverageVr.Value)
            .Where(room => !_maximumAverageVr.HasValue || room.AverageVr <= _maximumAverageVr.Value)
            .Where(room =>
                string.IsNullOrWhiteSpace(query)
                || room.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || room.GameMode.Contains(query, StringComparison.OrdinalIgnoreCase)
                || room.GameModeAbbrev.Contains(query, StringComparison.OrdinalIgnoreCase)
                || room.Players.Any(player =>
                    player.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || player.FriendCode.Contains(query, StringComparison.OrdinalIgnoreCase)
                )
            );

        filteredRooms = _averageVrSort switch
        {
            1 => filteredRooms.OrderBy(room => room.AverageVr),
            2 => filteredRooms.OrderByDescending(room => room.AverageVr),
            3 => filteredRooms.OrderBy(room => room.AverageVr).Take(1),
            4 => filteredRooms.OrderByDescending(room => room.AverageVr).Take(1),
            _ => filteredRooms,
        };

        var visibleRooms = filteredRooms.ToList();
        Rooms.Clear();
        foreach (var room in visibleRooms)
            Rooms.Add(room);

        Players.Clear();
        RoomsListItemCount.Text = visibleRooms.Count.ToString();
        RoomsListViewContainer.IsVisible = visibleRooms.Count != 0;
        PlayerListViewContainer.IsVisible = false;
        EmptyRoomsView.IsVisible = visibleRooms.Count == 0;
    }

    private void PerformSearch(string? query)
    {
        var isStringEmpty = string.IsNullOrWhiteSpace(query);
        RoomsListViewContainer.IsVisible = isStringEmpty;
        PlayerListViewContainer.IsVisible = !isStringEmpty;

        if (isStringEmpty)
            return;

        var safeQuery = query ?? string.Empty;
        Players.Clear();
        var matchingPlayers = Rooms
            .SelectMany(r => r.Players)
            .Where(p =>
                (p.Name?.Contains(safeQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.FriendCode?.Contains(safeQuery, StringComparison.OrdinalIgnoreCase) ?? false)
            )
            .Distinct()
            .ToList();

        foreach (var player in matchingPlayers)
            Players.Add(player);

        PlayerListItemCount.Text = matchingPlayers.Count.ToString();
    }

    private void RoomsPage_Unloaded(object? sender, RoutedEventArgs e)
    {
        _raceTimer.Stop();
        LiveRooms.Unsubscribe(this);
    }

    private void RaceTimer_OnTick(object? sender, EventArgs e)
    {
        foreach (var room in Rooms)
            room.RefreshRaceTime();
    }

    private async void RoomsSearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var input = await Popups
            .Create<TextInputWindow>()
            .SetMainText("Search rooms")
            .SetExtraText("Search by room ID, game mode, player name, or friend code.")
            .SetPlaceholderText("Room, player, or friend code")
            .SetInitialText(_searchQuery ?? string.Empty)
            .SetButtonText("Clear", "Search")
            .ShowDialog();

        _searchQuery = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
        ApplyFilters();
    }

    private async void RoomsVrFilterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var minInput = await Popups
            .Create<TextInputWindow>()
            .SetMainText("Minimum average VR")
            .SetExtraText("Leave blank to remove the minimum VR filter.")
            .SetPlaceholderText("Min avg. VR")
            .SetInitialText(_minimumAverageVr?.ToString() ?? string.Empty)
            .SetButtonText("Clear", "Next")
            .ShowDialog();
        _minimumAverageVr = ParseVrFilter(minInput);

        var maxInput = await Popups
            .Create<TextInputWindow>()
            .SetMainText("Maximum average VR")
            .SetExtraText("Leave blank to remove the maximum VR filter.")
            .SetPlaceholderText("Max avg. VR")
            .SetInitialText(_maximumAverageVr?.ToString() ?? string.Empty)
            .SetButtonText("Clear", "Apply")
            .ShowDialog();
        _maximumAverageVr = ParseVrFilter(maxInput);
        ApplyFilters();
    }

    private async void RoomsSortButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var selectedSort = await Popups.Create<RoomSortWindow>().AwaitAnswer();
        if (selectedSort == null)
            return;

        _averageVrSort = selectedSort.Value;
        ApplyFilters();
    }

    internal static int? ParseVrFilter(string? text) =>
        int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value) && value >= 0
            ? value
            : null;

    private void RoomsView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox listBox)
            return;
        if (listBox.SelectedItem is not RrRoom selectedRoom)
            return;

        Navigation.NavigateTo<RoomDetailsPage>(selectedRoom);
        listBox.SelectedItem = null;
        // Deselect the item immediately after navigating. This is important
        // for a good user experience. Otherwise, the item stays selected,
        // and if you navigate back, you can't re-select the same item.
    }

    private void PlayerView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox listBox)
            return;
        if (listBox.SelectedItem is not RrPlayer selectedRoom)
            return;

        var room = Rooms.FirstOrDefault(r => r.Players.Any(p => p.Equals(selectedRoom)));

        if (room is not null)
            Navigation.NavigateTo<RoomDetailsPage>(room);
        listBox.SelectedItem = null;
        // Deselect the item immediately after navigating. This is important
        // for a good user experience. Otherwise, the item stays selected,
        // and if you navigate back, you can't re-select the same item.
    }

    #region PropertyChanged

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    #endregion
}
