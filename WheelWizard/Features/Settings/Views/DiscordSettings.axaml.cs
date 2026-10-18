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

    private void Reload()
    {
        EnabledToggle.IsChecked = _settings.Get<bool>(_settings.DISCORD_PRESENCE_ENABLED);
        LiveInfoToggle.IsChecked = _settings.Get<bool>(_settings.DISCORD_LIVE_INFO);
        SetupButton.Text = HasSetup ? "Change setup" : "Run setup";

        if (!HasSetup)
        {
            SummaryText.Text = "Not set up yet. Press Run setup.";
            return;
        }

        var image = _settings.Get<string>(_settings.DISCORD_LARGE_IMAGE);
        SummaryText.Text =
            $"Application ID: {_settings.Get<string>(_settings.DISCORD_APP_ID)}{Environment.NewLine}"
            + $"Image: {(image.Length == 0 ? "none" : image)}";
    }

    private async void Setup_OnClick(object? sender, RoutedEventArgs e)
    {
        await _popups.Create<DiscordSetupWindow>().AwaitCompletion();
        Reload();
    }

    private void Enabled_OnClick(object? sender, RoutedEventArgs e)
    {
        var enabled = EnabledToggle.IsChecked == true;
        ResultText.IsVisible = true;
        if (enabled && !HasSetup)
        {
            EnabledToggle.IsChecked = false;
            ResultText.Text = "Run setup first.";
            return;
        }

        SettingsEditing.Set(_settings, _settings.DISCORD_PRESENCE_ENABLED, enabled);
        // Dolphin ignores the game's own presence while its Discord setting is off, so only the launcher's shows.
        SettingsEditing.Set(_settings, _settings.DOLPHIN_DISCORD_PRESENCE, !enabled);
        ResultText.Text = enabled ? "On. Restart Dolphin, then start the game." : "Off.";
    }

    private void LiveInfo_OnClick(object? sender, RoutedEventArgs e) =>
        SettingsEditing.Set(_settings, _settings.DISCORD_LIVE_INFO, LiveInfoToggle.IsChecked == true);

    private async void Check_OnClick(object? sender, RoutedEventArgs e)
    {
        CheckText.IsVisible = true;
        CheckText.Text = "Checking…";
        var game = await Task.Run(_presence.Diagnose);
        CheckText.Text = $"{game}{Environment.NewLine}{await _presence.CheckImageAsync()}";
    }
}
