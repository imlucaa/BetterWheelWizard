using WheelWizard.Themes;

namespace WheelWizard.Test.Features.Themes;

public class RandomThemeTests
{
    [Theory]
    [InlineData(0, 1, 1, "#FF0000")]
    [InlineData(120, 1, 1, "#00FF00")]
    [InlineData(240, 1, 1, "#0000FF")]
    [InlineData(60, 1, 1, "#FFFF00")]
    [InlineData(0, 0, 1, "#FFFFFF")]
    [InlineData(0, 0, 0, "#000000")]
    [InlineData(300, 1, 0.5, "#800080")]
    public void Hex_ConvertsKnownColors(double hue, double saturation, double value, string expected) =>
        Assert.Equal(expected, RandomTheme.Hex(hue, saturation, value));

    [Fact]
    public void Create_AlwaysGivesFourValidColors_WithReadableText()
    {
        var random = new Random(1234);
        for (var i = 0; i < 500; i++)
        {
            var theme = RandomTheme.Create(random);

            Assert.All([theme.Accent, theme.Branding, theme.Text, theme.Background], color => Assert.Matches("^#[0-9A-F]{6}$", color));
            Assert.True(Contrast(theme.Text, theme.Background) >= 7, $"{theme} has too little contrast");
        }
    }

    [Fact]
    public void Create_SameSeed_SameTheme() => Assert.Equal(RandomTheme.Create(new Random(7)), RandomTheme.Create(new Random(7)));

    private static double Contrast(string a, string b)
    {
        var (light, dark) = (Luminance(a), Luminance(b));
        if (light < dark)
            (light, dark) = (dark, light);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            var value = Convert.ToInt32(hex.Substring(start, 2), 16) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
