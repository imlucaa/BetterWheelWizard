using Avalonia.Controls;
using Avalonia.Interactivity;
using WheelWizard.DiscordPresence;
using WheelWizard.Views.Dialogs;

namespace WheelWizard.Settings.Views;

public partial class DiscordSettings : UserControl
{
    private readonly ISettingsManager _settings;
    private readonly DiscordPresenceService _presence;
    private readonly IPopupFactory _popups;

    public DiscordSettings(ISettingsManager settings, DiscordPresenceService presence, IPopupFactory popups)
    {
        _settings = settings;
        _presence = presence;
        _popups = popups;
        InitializeComponent();
        Reload();
    }

    private bool HasSetup => DiscordPresenceService.IsValidApplicationId(_settings.Get<string>(_settings.DISCORD_APP_ID));

    private bool _syncing;

    private const int ModeOff = 0;
    private const int ModeDefault = 1;
    private const int ModeCustom = 2;

    private bool CustomMode => ModeToggle.SelectedIndex == ModeCustom;

    private void Reload()
    {
        _syncing = true;
        // Custom needs a saved setup; without one the status is Dolphin's own default.
        var custom = HasSetup && _settings.Get<bool>(_settings.DISCORD_PRESENCE_ENABLED);
        var defaultStatus = !custom && _settings.Get<bool>(_settings.DOLPHIN_DISCORD_PRESENCE);
        ModeToggle.SelectedIndex =
            custom ? ModeCustom
            : defaultStatus ? ModeDefault
            : ModeOff;
        _syncing = false;

        ModeNote.Text =
            custom ? string.Empty
            : defaultStatus ? "Using the default Retro Rewind status."
            : "No Discord status at all.";
        ModeNote.IsVisible = ModeNote.Text.Length > 0;
        LiveInfoToggle.IsChecked = _settings.Get<bool>(_settings.DISCORD_LIVE_INFO);
        PlayerButtonToggle.IsChecked = _settings.Get<bool>(_settings.DISCORD_PLAYER_BUTTON);
        RoomButtonToggle.IsChecked = _settings.Get<bool>(_settings.DISCORD_ROOM_BUTTON);
        SetupButton.Text = HasSetup ? "Change" : "Set up";

        // Live info and the two buttons only exist in the custom status.
        LiveRow.IsVisible = custom;
        PlayerRow.IsVisible = custom;
        RoomRow.IsVisible = custom;
        SetupButton.IsVisible = custom;
        // The setup summary and check only mean something for the custom status.
        SetupBlock.IsVisible = custom;
        CheckButton.IsVisible = custom;
        PreviewButton.IsVisible = custom;

        if (!custom)
            return;

        var image = _settings.Get<string>(_settings.DISCORD_LARGE_IMAGE);
        SummaryText.Text =
            $"ID {_settings.Get<string>(_settings.DISCORD_APP_ID)}{Environment.NewLine}Image: {(image.Length == 0 ? "none" : image)}";
    }

    private async void Preview_OnClick(object? sender, RoutedEventArgs e)
    {
        await _popups
            .Create<DiscordPreviewWindow>(
                LiveInfoToggle.IsChecked == true,
                PlayerButtonToggle.IsChecked == true,
                RoomButtonToggle.IsChecked == true
            )
            .AwaitCompletion();
        Reload();
    }

    private async void Mode_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;

        ResultText.IsVisible = true;
        if (ModeToggle.SelectedIndex == ModeOff)
        {
            // Nothing at all: neither the launcher's status nor Dolphin's own. The saved setup stays for later.
            SettingsEditing.Set(_settings, _settings.DISCORD_PRESENCE_ENABLED, false);
            SettingsEditing.Set(_settings, _settings.DOLPHIN_DISCORD_PRESENCE, false);
            SetRecompPresence(false);
            _presence.DisableLiveDefault();
            Reload();
            // Dolphin only reads its setting at start, so a running one is told to drop its status right now.
            ResultText.Text = await _presence.ClearDolphinOwnStatusAsync()
                ? "Off."
                : "Off. If Dolphin is open and still shows a status, restart it.";
            return;
        }

        if (!CustomMode)
        {
            // Keeps the saved setup, but lets Dolphin show its own default status.
            SettingsEditing.Set(_settings, _settings.DISCORD_PRESENCE_ENABLED, false);
            SettingsEditing.Set(_settings, _settings.DOLPHIN_DISCORD_PRESENCE, true);
            SetRecompPresence(true);
            // A Dolphin that is already open will not start its own status until it restarts, so the launcher shows it.
            _presence.EnableLiveDefault();
            ResultText.IsVisible = false;
            Reload();
            return;
        }

        if (!HasSetup)
        {
            await _popups.Create<DiscordSetupWindow>().AwaitCompletion();
            if (!HasSetup)
            {
                Reload();
                return;
            }
        }

        SettingsEditing.Set(_settings, _settings.DISCORD_PRESENCE_ENABLED, true);
        SettingsEditing.Set(_settings, _settings.DOLPHIN_DISCORD_PRESENCE, false);
        SetRecompPresence(false);
        _presence.DisableLiveDefault();
        Reload();
        // Dolphin's own status would show next to ours until it restarts, so it is cleared right away.
        await _presence.ClearDolphinOwnStatusAsync();
        ResultText.Text = "Using your custom status.";
        _presence.RefreshNow();
    }

    // WiiCompiled has its own switch for the game's status in its Config.toml, read when it starts.
    private void SetRecompPresence(bool enabled)
    {
        if (_settings.IsRecompModeActive())
            SettingsEditing.Set(_settings, _settings.RECOMP_DISCORD_PRESENCE, enabled);
    }

    private async void Setup_OnClick(object? sender, RoutedEventArgs e)
    {
        await _popups.Create<DiscordSetupWindow>().AwaitCompletion();
        Reload();
    }

    private void PlayerButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsEditing.Set(_settings, _settings.DISCORD_PLAYER_BUTTON, PlayerButtonToggle.IsChecked == true);
        _presence.RefreshNow();
    }

    private void RoomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsEditing.Set(_settings, _settings.DISCORD_ROOM_BUTTON, RoomButtonToggle.IsChecked == true);
        _presence.RefreshNow();
    }

    private void LiveInfo_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsEditing.Set(_settings, _settings.DISCORD_LIVE_INFO, LiveInfoToggle.IsChecked == true);
        _presence.RefreshNow();
    }

    private async void Check_OnClick(object? sender, RoutedEventArgs e)
    {
        CheckText.IsVisible = true;
        CheckText.Text = "Checking…";
        var game = await Task.Run(_presence.Diagnose);
        CheckText.Text = $"{game}{Environment.NewLine}{await _presence.CheckImageAsync()}";
    }
}
