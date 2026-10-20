using Avalonia.Headless.XUnit;
using Avalonia.Media;
using WheelWizard.Themes;
using WheelWizard.Views.Dialogs;
using WheelWizard.Views.Pages;

namespace WheelWizard.UI.Test;

public class ThemePreviewTests
{
    private static readonly LauncherBackgroundState NoBackground = new(null, Stretch.UniformToFill, 1);

    [AvaloniaFact]
    public void Preview_OpensWithTheRealLauncherControls()
    {
        var theme = new LauncherThemeFile("Test", "#F04444", "#F04444", "#F8DADA", "#260909");

        var preview = new PreviewHomeWindow(theme, NoBackground, 0.8, minimalHomeArtwork: false, hideHomeTitle: false);

        Assert.True(preview.IsOpen);
    }

    [AvaloniaFact]
    public void Preview_FollowsNewColors_WithoutThrowing()
    {
        var preview = new PreviewHomeWindow(
            new LauncherThemeFile("A", "#F04444", "#F04444", "#F8DADA", "#260909"),
            NoBackground,
            0.8,
            false,
            false
        );

        preview.UpdatePreview(new LauncherThemeFile("B", "#249AF3", "#249AF3", "#F1F3F5", "#101216"), NoBackground, 0.5, true, true);

        Assert.True(preview.IsOpen);
    }
}
