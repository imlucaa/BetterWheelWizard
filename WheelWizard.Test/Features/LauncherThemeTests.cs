using Avalonia.Media;
using WheelWizard.Themes;

namespace WheelWizard.Test.Features;

public class LauncherThemeTests
{
    [Theory]
    [InlineData("#0000FF")]
    [InlineData("#FF0000")]
    [InlineData("#00FF00")]
    public void DarkBackground_PreservesHueAtReadableBrightness(string hex)
    {
        var background = LauncherThemeService.CreateDarkBackground(Color.Parse(hex));
        var channels = new[] { background.R, background.G, background.B };

        Assert.InRange(channels.Max(), 1, 72);
        Assert.True(channels.Max() > channels.Min());
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void DarkBackground_RemainsVisibleForMonochromeThemes(string hex)
    {
        var background = LauncherThemeService.CreateDarkBackground(Color.Parse(hex));

        Assert.True(background.R > 0 || background.G > 0 || background.B > 0);
        Assert.True(Math.Max(background.R, Math.Max(background.G, background.B)) < 64);
    }

    [Fact]
    public void TextContrast_AcceptsAccessibleCombination()
    {
        Assert.True(LauncherThemeService.HasReadableContrast(Color.Parse("#D3D3FF"), Color.Parse("#383840")));
    }

    [Fact]
    public void TextContrast_WarnsForBlackOnDarkBackground()
    {
        Assert.False(LauncherThemeService.HasReadableContrast(Color.Parse("#000000"), Color.Parse("#08090C")));
    }
}
