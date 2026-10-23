using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WheelWizard.CustomDistributions;
using WheelWizard.Models.Enums;
using WheelWizard.RrRooms;
using WheelWizard.Shared.Desktop.Storage;
using WheelWizard.Shared.Services;
using WheelWizard.Views.Dialogs;
using WheelWizard.Views.Shell;
using WheelWizard.WiiManagement.FriendCodes;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.Settings.Views;

/// <summary>
/// Checks a friend code and, once it passes, gives it to the license you pick. Nothing changes without a backup of
/// rksys.dat, and a code that is taken is refused.
/// </summary>
public partial class FriendCodeSettings : UserControl
{
    /// <summary>Every profile ID below this was already given out (community figure as of 8 September 2026).</summary>
    private const uint FirstFreeProfileId = 183_000;

    private const string DatabaseUrl = "https://github.com/ImpactCoding/rr-player-database/blob/main/rr-players.json";
    private const string GodtoolUrl = "https://gabrlel.github.io/godtool.html";

    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#ff6d7e"));
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#5ef0a5"));

    private readonly IApiCaller<IRwfcApi> _api;
    private readonly IGameLicenseSingletonService _licenses;
    private readonly IPopupFactory _popups;
    private readonly ISettingsManager _settings;
    private readonly ISaveRegionService _saveRegions;
    private readonly ICustomDistributionPaths _paths;
    private readonly List<MarioKartWiiEnums.Regions> _regions = [];
    private bool _syncingRegion;
    private readonly List<LicenseIdentity> _identities = [];
    private int _version;
    private int _selected;
    private string _shownProfileId = string.Empty;

    // The code that passed the last check; only this one can be applied.
    private uint? _approvedProfileId;
    private string _approvedCode = string.Empty;

    public FriendCodeSettings(
        IApiCaller<IRwfcApi> api,
        IGameLicenseSingletonService licenses,
        IPopupFactory popups,
        ISettingsManager settings,
        ISaveRegionService saveRegions,
        ICustomDistributionPaths paths
    )
    {
        _settings = settings;
        _saveRegions = saveRegions;
        _paths = paths;
        _api = api;
        _licenses = licenses;
        _popups = popups;
        InitializeComponent();
        AckBox.IsCheckedChanged += (_, _) => UpdateApplyButton();
        CodeInput.TextChanged += (_, _) => HideApply();
        RegionPicker.SelectionChanged += RegionPicker_OnChanged;
        RefreshRegions();
        RefreshLicenses();
        RefreshUndo();
    }

    #region Region

    // Every region the game came in, with the disc code its save folder is named after.
    private static readonly MarioKartWiiEnums.Regions[] AllRegions =
    [
        MarioKartWiiEnums.Regions.America,
        MarioKartWiiEnums.Regions.Europe,
        MarioKartWiiEnums.Regions.Japan,
        MarioKartWiiEnums.Regions.Korea,
    ];

    private static string RegionName(MarioKartWiiEnums.Regions region) =>
        region switch
        {
            MarioKartWiiEnums.Regions.America => "America (RMCE)",
            MarioKartWiiEnums.Regions.Europe => "Europe (RMCP)",
            MarioKartWiiEnums.Regions.Japan => "Japan (RMCJ)",
            MarioKartWiiEnums.Regions.Korea => "South Korea (RMCK)",
            _ => "None",
        };

    private readonly HashSet<MarioKartWiiEnums.Regions> _regionsWithSave = [];

    private void RefreshRegions()
    {
        _syncingRegion = true;
        _regionsWithSave.Clear();
        try
        {
            foreach (var region in _saveRegions.GetAvailableRegions(_paths.SaveFolderPath))
                _regionsWithSave.Add(region);
        }
        catch (Exception)
        {
            // No save folder yet.
        }

        _regions.Clear();
        _regions.AddRange(AllRegions);
        RegionPicker.ItemsSource = _regions
            .Select(r => _regionsWithSave.Contains(r) ? RegionName(r) : RegionName(r) + " - no save")
            .ToList();
        var current = _settings.Get<MarioKartWiiEnums.Regions>(_settings.RR_REGION);
        RegionPicker.SelectedIndex = _regions.IndexOf(current);
        _syncingRegion = false;
    }

    private void RegionPicker_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingRegion || RegionPicker.SelectedIndex < 0 || RegionPicker.SelectedIndex >= _regions.Count)
            return;

        var region = _regions[RegionPicker.SelectedIndex];
        if (!_regionsWithSave.Contains(region))
        {
            // Nothing to edit yet: the game makes the save the first time that disc is played.
            ViewUtils.ShowSnackbar(
                $"No {RegionName(region)} save yet. Play that disc once and it appears.",
                ViewUtils.SnackbarType.Warning
            );
            RefreshRegions();
            return;
        }

        _settings.Set(_settings.RR_REGION, region);
        _licenses.LoadLicense();
        AfterSaveChanged();
    }

    #endregion

    #region Licenses

    private void RefreshLicenses()
    {
        _identities.Clear();
        try
        {
            _identities.AddRange(_licenses.GetLicenseIdentities());
        }
        catch (Exception)
        {
            // No save loaded yet.
        }

        if (_selected >= _identities.Count)
            _selected = 0;
        RebuildLicenseList();
        LicenseNote.Text = _identities.Count == 0 ? "No licenses found. Start the game once first." : string.Empty;
        LicenseNote.IsVisible = _identities.Count == 0;
        UpdateApplyButton();
    }

    private void RebuildLicenseList()
    {
        LicenseList.Children.Clear();
        for (var i = 0; i < _identities.Count; i++)
        {
            var index = i;
            var license = _identities[i];
            var clashes = _identities
                .Where(o =>
                    o.Slot != license.Slot
                    && (o.ProfileId == license.ProfileId || o.PseudoId == license.PseudoId || o.AuthenticId == license.AuthenticId)
                )
                .Select(Label)
                .Distinct()
                .ToList();
            var selected = index == _selected;

            // Name small on top, the friend code under it; a red line only when IDs clash.
            var row = new StackPanel { Spacing = 1 };
            if (HasName(license))
                row.Children.Add(
                    new TextBlock
                    {
                        Text = license.Name,
                        FontSize = 11,
                        Opacity = 0.7,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    }
                );
            row.Children.Add(
                new TextBlock
                {
                    Text = license.FriendCode,
                    FontWeight = selected ? FontWeight.Bold : FontWeight.Medium,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                }
            );
            if (clashes.Count > 0)
            {
                var status = new TextBlock
                {
                    Text = "Duplicate IDs",
                    FontSize = 11,
                    Foreground = Bad,
                };
                ToolTip.SetTip(status, "Shares IDs with " + string.Join(", ", clashes) + ".");
                row.Children.Add(status);
            }

            var tile = new Border
            {
                Margin = new Thickness(3),
                Padding = new Thickness(10, 7),
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = new SolidColorBrush(Color.Parse(selected ? "#30ffffff" : "#14ffffff")),
                BorderThickness = new Thickness(selected ? 2 : 1),
                BorderBrush = new SolidColorBrush(Color.Parse(selected ? "#ccffffff" : "#18ffffff")),
                Child = row,
            };
            tile.PointerPressed += (_, _) =>
            {
                _selected = index;
                RebuildLicenseList();
                UpdateApplyButton();
            };
            LicenseList.Children.Add(tile);
        }
    }

    #endregion

    #region Check

    private void CodeInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Check_OnClick(sender, e);
    }

    private void HideApply()
    {
        _approvedProfileId = null;
        ApplyPanel.IsVisible = false;
    }

    // A license without a name (the game calls it "no name") goes by its friend code instead.
    private static bool HasName(LicenseIdentity license) => !string.IsNullOrWhiteSpace(license.Name) && license.Name != "no name";

    private static string Label(LicenseIdentity license) => HasName(license) ? license.Name : license.FriendCode;

    private void UpdateApplyButton()
    {
        var name = _selected >= 0 && _selected < _identities.Count ? Label(_identities[_selected]) : string.Empty;
        ApplyButton.Text = name.Length == 0 ? "Replace" : $"Replace {name}";
        ApplyButton.IsEnabled = _approvedProfileId is not null && AckBox.IsChecked == true && name.Length > 0;
    }

    private async void Check_OnClick(object? sender, RoutedEventArgs e)
    {
        var version = ++_version;
        HideApply();
        var digits = new string((CodeInput.Text ?? string.Empty).Where(char.IsDigit).ToArray());
        ResultPanel.IsVisible = true;
        CopyIdButton.IsVisible = false;
        ResultDetail.Text = string.Empty;
        ResultVerdict.Text = string.Empty;

        if (digits.Length == 0 || digits.Length > 12)
        {
            ResultCode.Text = "Type a friend code or a profile ID.";
            return;
        }

        uint profileId;
        ulong value;
        bool valid;
        if (digits.Length == 12)
        {
            value = ulong.Parse(digits);
            profileId = (uint)(value & 0xFFFFFFFF);
            valid = profileId != 0 && FriendCode.ProfileIdToFriendCode(profileId) == value;
        }
        else
        {
            if (!uint.TryParse(digits, out profileId) || profileId == 0)
            {
                ResultCode.Text = "That is not a profile ID.";
                return;
            }

            value = FriendCode.ProfileIdToFriendCode(profileId);
            valid = true;
        }

        var text = value.ToString("D12");
        var code = $"{text[..4]}-{text.Substring(4, 4)}-{text.Substring(8, 4)}";
        ResultCode.Text = $"{code}   ID {profileId}";
        _shownProfileId = profileId.ToString();
        CopyIdButton.IsVisible = true;

        if (!valid)
        {
            ResultVerdict.Foreground = Bad;
            ResultVerdict.Text = "Not a valid friend code.";
            return;
        }

        if (profileId < FirstFreeProfileId)
        {
            ResultVerdict.Foreground = Bad;
            ResultVerdict.Text = "Taken. IDs below 183,000 are all in use.";
            return;
        }

        if (_identities.FirstOrDefault(i => i.ProfileId == profileId) is { } mine)
        {
            ResultVerdict.Foreground = Bad;
            ResultVerdict.Text = $"{Label(mine)} already has this code.";
            return;
        }

        ResultDetail.Text = "Checking rwfc.net...";
        var result = await _api.CallApiAsync(api => api.GetPlayerProfileAsync(code));
        if (version != _version)
            return;

        // A player with a blank name (only spaces) still exists, so go by the profile, not the name.
        var found = result.IsSuccess && result.Value is not null && (result.Value.Pid.Length > 0 || result.Value.FriendCode.Length > 0);
        if (found)
        {
            var name = string.IsNullOrWhiteSpace(result.Value!.Name) ? "a player" : result.Value.Name;
            ResultDetail.Text = $"{name}, {result.Value.Vr:N0} VR, rank {result.Value.Rank:N0}";
            ResultVerdict.Foreground = Bad;
            ResultVerdict.Text = "Taken. This code belongs to a player.";
            return;
        }

        ResultDetail.Text = string.Empty;
        ResultVerdict.Foreground = Good;
        ResultVerdict.Text = "Looks free on rwfc.net.";
        _approvedProfileId = profileId;
        _approvedCode = code;
        AckBox.IsChecked = false;
        ApplyPanel.IsVisible = _identities.Count > 0;
        UpdateApplyButton();
    }

    #endregion

    #region Replace

    /// <summary>
    /// Dolphin and WiiCompiled keep the save in memory while the game runs and write it back, so a change made then is
    /// lost.
    /// </summary>
    internal static bool IsGameRunning()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            return processes.Any(p =>
            {
                try
                {
                    var name = p.ProcessName;
                    return name.StartsWith("Dolphin", StringComparison.OrdinalIgnoreCase)
                            && !name.Contains("Tool", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("dolphin-emu", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("WiiCompiled", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("RetroRewind", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    // The process ended while the list was read.
                    return false;
                }
            });
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    internal static bool BlockWhileGameRuns()
    {
        if (!IsGameRunning())
            return false;

        ViewUtils.ShowSnackbar("Close the game first. It would write its old save back over the change.", ViewUtils.SnackbarType.Warning);
        return true;
    }

    private async void Apply_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_approvedProfileId is not { } profileId || AckBox.IsChecked != true)
            return;
        if (BlockWhileGameRuns())
            return;
        if (_selected < 0 || _selected >= _identities.Count)
            return;

        var license = _identities[_selected];
        var yes = await _popups
            .Create<YesNoWindow>()
            .SetMainText($"Replace the code of {Label(license)}?")
            .SetExtraText(
                $"{license.FriendCode} becomes {_approvedCode}. It is set on all your regions' saves, which are backed up first. Close the game before you continue."
            )
            .AwaitAnswer();
        if (!yes)
            return;

        var result = _licenses.ChangeProfileId(license.Slot, profileId);
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(result.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        ViewUtils.ShowSnackbar($"Friend code changed to {_approvedCode}.");
        // The sidebar and the friends count keep their own copy of the active license, so refresh them.
        ViewUtils.GetLayout().UpdateSidebarProfile();
        ViewUtils.GetLayout().UpdateFriendCount();
        HideApply();
        RefreshLicenses();
        RefreshUndo();
    }

    #endregion

    private async void CopyId_OnClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || _shownProfileId.Length == 0)
            return;

        await clipboard.SetTextAsync(_shownProfileId);
        ViewUtils.ShowSnackbar("Profile ID copied.");
    }

    private void AfterSaveChanged()
    {
        ViewUtils.GetLayout().UpdateSidebarProfile();
        ViewUtils.GetLayout().UpdateFriendCount();
        HideApply();
        RefreshLicenses();
        RefreshUndo();
    }

    /// <summary>Shows the undo row when there is a backup from an earlier change.</summary>
    private void RefreshUndo()
    {
        DateTime? when = null;
        try
        {
            when = _licenses.GetLastBackupTime();
        }
        catch (Exception)
        {
            // No save folder yet.
        }

        UndoCard.IsVisible = when is not null;
        UndoText.Text = when is { } time ? $"Undo goes back to {time:d MMM, HH:mm}." : string.Empty;
    }

    private async void Undo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_licenses.GetLastBackupTime() is not { } when)
            return;
        if (BlockWhileGameRuns())
            return;

        var yes = await _popups
            .Create<YesNoWindow>()
            .SetMainText("Undo the friend code change?")
            .SetExtraText(
                $"The friend codes go back to how they were on {when:d MMM, HH:mm}. Your races, VR and Miis stay as they are. Close the game before you continue."
            )
            .AwaitAnswer();
        if (!yes)
            return;

        var result = _licenses.UndoLastProfileIdChange();
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(result.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        ViewUtils.ShowSnackbar("Friend codes put back.");
        ViewUtils.GetLayout().UpdateSidebarProfile();
        ViewUtils.GetLayout().UpdateFriendCount();
        HideApply();
        RefreshLicenses();
        RefreshUndo();
    }

    private void Database_OnClick(object? sender, RoutedEventArgs e) => ViewUtils.OpenLink(DatabaseUrl);

    private void Godtool_OnClick(object? sender, RoutedEventArgs e) => ViewUtils.OpenLink(GodtoolUrl);
}
