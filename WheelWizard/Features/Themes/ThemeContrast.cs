using Avalonia.Media;

namespace WheelWizard.Themes;

/// <summary>What is hard to read in a set of theme colors, and colors that would fix it.</summary>
/// <param name="Problems">One short sentence for each color that is hard to see.</param>
/// <param name="Accent">The main color to use instead (the same one when it was fine).</param>
/// <param name="Branding">The title color to use instead (the same one when it was fine).</param>
/// <param name="Text">The text color to use instead (the same one when it was fine).</param>
public sealed record ContrastReport(IReadOnlyList<string> Problems, string Accent, string Branding, string Text);

/// <summary>Checks how well the four theme colors stand out from the background, using the WCAG contrast ratio.</summary>
public static class ThemeContrast
{
    /// <summary>Body text should reach this ratio to be comfortable to read.</summary>
    public const double TextMinimum = 4.5;

    /// <summary>Large or bold things, such as titles and buttons, only need this ratio.</summary>
    public const double ColorMinimum = 3.0;

    /// <summary>The problems with these colors, or null when nothing is hard to read (or a color is not a valid #RRGGBB).</summary>
    public static ContrastReport? Check(string accent, string branding, string text, string background)
    {
        if (
            !TryParse(accent, out var accentColor)
            || !TryParse(branding, out var brandingColor)
            || !TryParse(text, out var textColor)
            || !TryParse(background, out var backgroundColor)
        )
            return null;

        var problems = new List<string>();
        var fixedAccent = Fix("Main color", accentColor, backgroundColor, ColorMinimum, problems);
        var fixedBranding = Fix("Title color", brandingColor, backgroundColor, ColorMinimum, problems);
        var fixedText = Fix("Text color", textColor, backgroundColor, TextMinimum, problems);
        return problems.Count == 0 ? null : new ContrastReport(problems, fixedAccent, fixedBranding, fixedText);
    }

    private static string Fix(string name, Color color, Color background, double minimum, List<string> problems)
    {
        var ratio = LauncherThemeService.ContrastRatio(color, background);
        if (ratio >= minimum)
            return ToHex(color);

        problems.Add($"{name} is hard to see on this background ({ratio:0.0} to 1, it should be at least {minimum:0.#}).");
        return ToHex(LauncherThemeService.CreateReadableText(color, background));
    }

    private static bool TryParse(string? hex, out Color color)
    {
        color = default;
        return LauncherThemeService.IsValidHexColor(hex ?? string.Empty) && Color.TryParse(hex, out color);
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
