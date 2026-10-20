using WheelWizard.Settings;
using WheelWizard.Settings.Views;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.StreamOverlay;

/// <summary>
/// Keeps one steady OBS link that follows the friend code and look chosen in Wheel Wizard. The link is a tiny local page that
/// shows the hosted overlay and swaps it when the choice changes, so a license can be switched while playing.
/// </summary>
public sealed class OverlayLive(ISettingsManager settings, IGameLicenseSingletonService licenses)
{
    public static string LocalUrl => $"http://127.0.0.1:{DiscordPresence.RoomLinkListener.Port}/overlay";

    public const string Html = """
        <!doctype html><meta charset="utf-8"><title>VR overlay</title>
        <style>html,body{margin:0;height:100%;background:transparent;overflow:hidden}iframe{border:0;width:100%;height:100%;background:transparent}</style>
        <iframe id="f" allowtransparency="true"></iframe>
        <script>
        let current = '';
        async function tick() {
          try {
            const response = await fetch('/overlay/state', { cache: 'no-store' });
            const state = await response.json();
            if (state.url && state.url !== current) { current = state.url; document.getElementById('f').src = state.url; }
          } catch (e) { }
          setTimeout(tick, 1500);
        }
        tick();
        </script>
        """;

    /// <summary>The friend code picked in the VR overlay tab, else the active license.</summary>
    public string FriendCodeDigits()
    {
        var saved = Digits(settings.Get<string>(settings.OVERLAY_FRIEND_CODE));
        if (saved.Length == 12)
            return saved;

        try
        {
            return Digits(licenses.ActiveUser?.FriendCode);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void SaveFriendCode(string digits) => SettingsEditing.Set(settings, settings.OVERLAY_FRIEND_CODE, digits);

    /// <summary>The hosted overlay link for the current friend code and look, or null without a friend code.</summary>
    public string? CurrentUrl()
    {
        var digits = FriendCodeDigits();
        return digits.Length == 12 ? new OverlayConfig(settings).BuildLink(digits) : null;
    }

    private static string Digits(string? text) => new((text ?? string.Empty).Where(char.IsDigit).ToArray());
}
