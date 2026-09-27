using Avalonia.Media;
using WheelWizard.Themes;

namespace WheelWizard.Test.Features.Themes;

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

    [Fact]
    public void TextScale_UsesSelectedTextColorForNormalUiText()
    {
        var selected = Color.Parse("#FFFFFF");

        var scale = LauncherThemeService.CreateTextScale(selected, Colors.Black);

        Assert.Equal(selected, scale[4]);
    }
}
