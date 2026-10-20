using Avalonia.Media;
using WheelWizard.Themes;

namespace WheelWizard.Test.Features.Themes;

public class ThemeContrastTests
{
    [Fact]
    public void ContrastRatio_IsTheWcagRatio()
    {
        Assert.Equal(21, LauncherThemeService.ContrastRatio(Colors.White, Colors.Black), 3);
        Assert.Equal(1, LauncherThemeService.ContrastRatio(Colors.Gray, Colors.Gray), 3);
        Assert.Equal(
            LauncherThemeService.ContrastRatio(Colors.White, Colors.Navy),
            LauncherThemeService.ContrastRatio(Colors.Navy, Colors.White),
            6
        );
    }

    [Fact]
    public void Check_WithReadableColors_HasNothingToReport() =>
        Assert.Null(ThemeContrast.Check("#249AF3", "#F9A8D4", "#F1F3F5", "#101216"));

    [Fact]
    public void Check_WithInvalidColors_HasNothingToReport()
    {
        Assert.Null(ThemeContrast.Check("nope", "#FFFFFF", "#FFFFFF", "#000000"));
        Assert.Null(ThemeContrast.Check("#FFFFFF", "#FFFFFF", "#FFFFFF", "#12"));
    }

    [Fact]
    public void Check_WithDarkTextOnADarkBackground_ReportsTheTextAndSuggestsAReadableColor()
    {
        var report = ThemeContrast.Check("#249AF3", "#F9A8D4", "#202830", "#101216");

        Assert.NotNull(report);
        var problem = Assert.Single(report.Problems);
        Assert.StartsWith("Text color", problem);
        Assert.NotEqual("#202830", report.Text);
        Assert.True(Ratio(report.Text, "#101216") >= ThemeContrast.TextMinimum);
        Assert.Equal("#249AF3", report.Accent);
        Assert.Equal("#F9A8D4", report.Branding);
    }

    [Fact]
    public void Check_WithLightTextOnALightBackground_SuggestsDarkerText()
    {
        var report = ThemeContrast.Check("#7C3AED", "#5B21B6", "#EEEEEE", "#FAFAFA");

        Assert.NotNull(report);
        Assert.True(Ratio(report.Text, "#FAFAFA") >= ThemeContrast.TextMinimum);
        Assert.True(Lightness(report.Text) < Lightness("#EEEEEE"));
    }

    [Fact]
    public void Check_ReportsEachHardToSeeColorSeparately()
    {
        var report = ThemeContrast.Check("#111111", "#101216", "#1A1A1A", "#101216");

        Assert.NotNull(report);
        Assert.Equal(3, report.Problems.Count);
        Assert.Contains(report.Problems, p => p.StartsWith("Main color"));
        Assert.Contains(report.Problems, p => p.StartsWith("Title color"));
        Assert.Contains(report.Problems, p => p.StartsWith("Text color"));
        Assert.True(Ratio(report.Accent, "#101216") >= ThemeContrast.TextMinimum);
        Assert.True(Ratio(report.Branding, "#101216") >= ThemeContrast.TextMinimum);
    }

    [Fact]
    public void Suggestion_KeepsTheColorsOwnShade_InsteadOfJumpingToPureWhite()
    {
        var report = ThemeContrast.Check("#249AF3", "#F9A8D4", "#2A4058", "#101216");

        Assert.NotNull(report);
        // A blue-ish text color is lightened, not replaced: blue still leads.
        var channels = Channels(report.Text);
        Assert.True(channels.B >= channels.R);
    }

    private static double Ratio(string a, string b) => LauncherThemeService.ContrastRatio(Color.Parse(a), Color.Parse(b));

    private static double Lightness(string hex)
    {
        var (r, g, b) = Channels(hex);
        return (r + g + b) / 3.0;
    }

    private static (int R, int G, int B) Channels(string hex) =>
        (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
}
