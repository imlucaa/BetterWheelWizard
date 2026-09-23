using WheelWizard.Views.Pages;

namespace WheelWizard.Test.Features;

public class LauncherThemeFileTests
{
    [Fact]
    public void ThemeFile_AcceptsNamedThemeWithValidColors()
    {
        var theme = new LauncherThemeFile("Ocean", "#249AF3", "#DDF2FF", "#DDF2FF", "#071A2D");

        Assert.True(theme.HasValidNameAndColors());
    }

    [Theory]
    [InlineData("", "#249AF3", "#FFFFFF", "#FFFFFF", "#000000")]
    [InlineData("Broken", "249AF3", "#FFFFFF", "#FFFFFF", "#000000")]
    [InlineData("Broken", "#249AF3", "#GGFFFF", "#FFFFFF", "#000000")]
    [InlineData("Broken", "#249AF3", "#FFFFFF", "#FFFFFF", "#00000")]
    public void ThemeFile_RejectsMissingNameOrInvalidColors(string name, string accent, string branding, string text, string background)
    {
        var theme = new LauncherThemeFile(name, accent, branding, text, background);

        Assert.False(theme.HasValidNameAndColors());
    }

    [Fact]
    public void ShareCode_RoundTripsFourThemeColors()
    {
        var original = new LauncherThemeFile("Ocean", "#249AF3", "#DDF2FF", "#D7EFFF", "#071A2D");

        var code = LauncherThemeShareCode.Encode(original);
        var decoded = LauncherThemeShareCode.TryDecode(code, out var result);

        Assert.True(decoded);
        Assert.Equal("BWW1:249AF3:DDF2FF:D7EFFF:071A2D", code);
        Assert.Equal(original.Accent, result.Accent);
        Assert.Equal(original.Branding, result.Branding);
        Assert.Equal(original.Text, result.Text);
        Assert.Equal(original.Background, result.Background);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("249AF3:DDF2FF:D7EFFF:071A2D")]
    [InlineData("BWW2:249AF3:DDF2FF:D7EFFF:071A2D")]
    [InlineData("BWW1:249AF3:NOTHEX:D7EFFF:071A2D")]
    [InlineData("BWW1:249AF3:DDF2FF:D7EFFF")]
    public void ShareCode_RejectsInvalidInput(string? code)
    {
        Assert.False(LauncherThemeShareCode.TryDecode(code, out _));
    }
}
