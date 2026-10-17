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

    [Fact]
    public void AccentResourceKeys_IncludeV261BrandPalette()
    {
        Assert.Contains("Brand400", LauncherThemeService.ThemeAccentResourceKeys);
        Assert.Contains("Primary400", LauncherThemeService.ThemeAccentResourceKeys);
    }

    [Fact]
    public void AccentText_RemainsReadableForBrightThemeColor()
    {
        var accent = Color.Parse("#FFFF00");

        var text = LauncherThemeService.CreateReadableText(Colors.White, accent);

        Assert.True(LauncherThemeService.HasReadableContrast(text, accent));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 128)]
    [InlineData(1.0, 255)]
    public void LauncherSurfaceOpacity_MapsToBrushAlpha(double opacity, byte expectedAlpha)
    {
        var surface = LauncherThemeService.ApplyOpacity(Color.Parse("#101216"), opacity);

        Assert.Equal(expectedAlpha, surface.A);
    }

    [Theory]
    [InlineData(0.0, 89)]
    [InlineData(0.5, 172)]
    [InlineData(1.0, 255)]
    public void ReadableSurface_FollowsPanelOpacityWithAReadabilityFloor(double opacity, byte expectedAlpha)
    {
        var background = Color.Parse("#101216");

        var surface = LauncherThemeService.CreateReadableSurface(background, opacity);

        Assert.Equal(expectedAlpha, surface.A);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 128)]
    [InlineData(1.0, 255)]
    public void SidebarBoxSurface_FollowsPanelOpacity(double opacity, byte expectedAlpha)
    {
        var surface = LauncherThemeService.CreateSidebarBoxSurface(Color.Parse("#000000"), opacity, false);

        Assert.Equal(expectedAlpha, surface.A);
    }

    [Fact]
    public void SidebarBoxHover_KeepsPanelOpacity()
    {
        var normal = LauncherThemeService.CreateSidebarBoxSurface(Color.Parse("#000000"), 0.4, false);
        var hover = LauncherThemeService.CreateSidebarBoxSurface(Color.Parse("#000000"), 0.4, true);

        Assert.Equal(normal.A, hover.A);
        Assert.NotEqual(normal, hover);
    }

    [Theory]
    [InlineData("#FFFFFF", "#000000")]
    [InlineData("#FFD80D", "#000000")]
    [InlineData("#249AF3", "#000000")]
    [InlineData("#FF8A3D", "#000000")]
    [InlineData("#F04444", "#FFFFFF")]
    [InlineData("#000000", "#FFFFFF")]
    [InlineData("#7C3AED", "#FFFFFF")]
    public void AccentText_IsCrispBlackOrWhite(string accentHex, string expectedHex)
    {
        var text = LauncherThemeService.CreateAccentText(Color.Parse(accentHex));

        Assert.Equal(Color.Parse(expectedHex), text);
    }
}
