using System.Text.RegularExpressions;

namespace WheelWizard.AutoUpdating;

/// <summary>Turns a GitHub release description (markdown) into a short plain-text summary for the update prompt.</summary>
public static partial class ReleaseNotesText
{
    private const int DefaultMaxLength = 600;

    public static string Summarize(string? markdown, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = new List<string>();
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("<!--", StringComparison.Ordinal))
                continue;

            var heading = HeadingMarks().IsMatch(line);
            line = HeadingMarks().Replace(line, string.Empty);
            // The update prompt puts its own "What's new:" above the notes, so the same heading is not shown twice.
            if (heading && line.TrimEnd(':').Trim().Equals("What's new", StringComparison.OrdinalIgnoreCase))
                continue;
            line = BulletMarks().Replace(line, "• ");
            line = Links().Replace(line, "$1");
            line = line.Replace("**", string.Empty).Replace("__", string.Empty).Replace("`", string.Empty).Trim();
            if (line.Length > 0)
                lines.Add(line);
        }

        var text = string.Join("\n", lines);
        if (text.Length <= maxLength)
            return text;

        // Cut at the last whole line that fits, so a bullet is never chopped in half.
        var cut = text.LastIndexOf('\n', maxLength);
        return (cut > 0 ? text[..cut] : text[..maxLength]).TrimEnd() + "\n…";
    }

    [GeneratedRegex(@"^#{1,6}\s*")]
    private static partial Regex HeadingMarks();

    [GeneratedRegex(@"^[-*+]\s+")]
    private static partial Regex BulletMarks();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex Links();
}
