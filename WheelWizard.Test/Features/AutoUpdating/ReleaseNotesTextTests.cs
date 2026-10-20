using WheelWizard.AutoUpdating;

namespace WheelWizard.Test.Features.AutoUpdating;

public class ReleaseNotesTextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Summarize_WithNothing_IsEmpty(string? body) => Assert.Equal(string.Empty, ReleaseNotesText.Summarize(body));

    [Fact]
    public void Summarize_StripsMarkdown_ButKeepsTheWords()
    {
        var text = ReleaseNotesText.Summarize(
            "## What's new\r\n- **Discord** preview, see [docs](https://example.com)\r\n* Korea `RMCK` support\r\n"
        );

        Assert.Equal("What's new\n• Discord preview, see docs\n• Korea RMCK support", text);
    }

    [Fact]
    public void Summarize_DropsCodeFencesAndComments()
    {
        var text = ReleaseNotesText.Summarize("<!-- hidden -->\n```\n- line\n```\nReal line");

        Assert.Equal("• line\nReal line", text);
    }

    [Fact]
    public void Summarize_WhenTooLong_CutsAtAWholeLineAndAddsAnEllipsis()
    {
        var body = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"- change number {i}"));

        var text = ReleaseNotesText.Summarize(body, 100);

        Assert.True(text.Length <= 102);
        Assert.EndsWith("\n…", text);
        Assert.DoesNotContain("change number 40", text);
        Assert.All(text.Split('\n')[..^1], line => Assert.Matches(@"^• change number \d+$", line));
    }
}
