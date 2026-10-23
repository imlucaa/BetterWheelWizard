using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NSubstitute;
using WheelWizard.ApplicationData;
using WheelWizard.RrRooms;
using WheelWizard.Settings;
using WheelWizard.Shared.Services;
using WheelWizard.StreamOverlay;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.UI.Test;

public class OverlayWindowTests
{
    [AvaloniaFact]
    public void OpenAsWindow_ShowsOneFramelessOnTopWindow_WithTheOverlayCard()
    {
        var settings = Substitute.For<ISettingsManager>();
        var config = new OverlayConfig(settings);
        var live = new OverlayLive(settings, Substitute.For<IGameLicenseSingletonService>());
        var background = new OverlayBackground(Substitute.For<IApplicationDataLocation>());
        var api = Substitute.For<IApiCaller<IRwfcApi>>();

        OverlayWindow.ShowOrActivate(config, api, live, background);
        var window = OverlayWindow.Current!;
        // Pressing the button again brings the same window forward instead of opening a second one.
        OverlayWindow.ShowOrActivate(config, api, live, background);
        Assert.Same(window, OverlayWindow.Current);

        Assert.True(window.IsVisible);
        Assert.True(window.Topmost);
        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.IsType<OverlayPreview>(window.Content);

        // Closing it tells the page, so its button can go back to "Open as window", and lets a new one open next time.
        var changes = 0;
        OverlayWindow.OpenChanged += Count;
        void Count() => changes++;
        OverlayWindow.CloseIfOpen();
        OverlayWindow.OpenChanged -= Count;
        Assert.Equal(1, changes);
        Assert.Null(OverlayWindow.Current);
        OverlayWindow.ShowOrActivate(config, api, live, background);
        Assert.NotSame(window, OverlayWindow.Current);
        OverlayWindow.Current!.Close();
    }
}
