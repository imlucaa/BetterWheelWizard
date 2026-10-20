namespace WheelWizard.Themes;

/// <summary>
/// Makes a random set of four launcher colors that still look like a theme: one hue for everything, a bright main color,
/// a lighter title color a little further around the color wheel, near-white text and a near-black background. The text is
/// always light on dark, so a random theme stays readable.
/// </summary>
public static class RandomTheme
{
    public readonly record struct Colors(string Accent, string Branding, string Text, string Background);

    public static Colors Create(Random random)
    {
        var hue = random.NextDouble() * 360;
        return new Colors(
            Hex(hue, Between(random, 0.6, 0.85), Between(random, 0.85, 1.0)),
            Hex((hue + Between(random, 25, 60)) % 360, Between(random, 0.4, 0.7), Between(random, 0.9, 1.0)),
            Hex(hue, Between(random, 0.03, 0.1), 0.96),
            Hex(hue, Between(random, 0.3, 0.5), Between(random, 0.06, 0.12))
        );
    }

    private static double Between(Random random, double min, double max) => min + random.NextDouble() * (max - min);

    /// <summary>"#RRGGBB" for a hue in degrees (0 to 360), saturation and value (0 to 1).</summary>
    public static string Hex(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var segment = hue / 60 % 6;
        var second = chroma * (1 - Math.Abs(segment % 2 - 1));
        var (r, g, b) = (int)segment switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };
        var match = value - chroma;
        return $"#{ToByte(r + match):X2}{ToByte(g + match):X2}{ToByte(b + match):X2}";
    }

    private static int ToByte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);
}
