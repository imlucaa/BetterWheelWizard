using Avalonia.Media;
using WheelWizard.Themes;

namespace WheelWizard.Test.Features;

public class LauncherThemeTests
{
    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void AccentScale_PreservesSelectedBlackOrWhite(string hex)
    {
        var selected = Color.Parse(hex);
        var scale = LauncherThemeService.CreateAccentScale(selected);

        Assert.Equal(selected, scale[4]);
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

    [Fact]
    public void ReadableText_DarkensTextForBrightBackground()
    {
        var background = Color.Parse("#C8B1DE");
        var text = LauncherThemeService.CreateReadableText(Colors.White, background);

        Assert.True(LauncherThemeService.HasReadableContrast(text, background));
        Assert.True(text.R < 128 && text.G < 128 && text.B < 128);
    }

    [Fact]
    public void ReadableText_LightensTextForDarkBackground()
    {
        var background = Color.Parse("#101216");
        var text = LauncherThemeService.CreateReadableText(Colors.Black, background);

        Assert.True(LauncherThemeService.HasReadableContrast(text, background));
        Assert.True(text.R > 96 && text.G > 96 && text.B > 96);
    }
}
