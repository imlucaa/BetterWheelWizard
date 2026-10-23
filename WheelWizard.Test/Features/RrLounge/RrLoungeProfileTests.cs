using WheelWizard.RrLounge;

namespace WheelWizard.Test.Features.RrLounge;

public class RrLoungeProfileTests
{
    // A cut-down copy of a player page on rr-lounge.com, which is plain HTML.
    public const string Page =
        @"<html><body><div><img src=""/icons/new_diamond.png"" alt=""Diamond"" width=""100"" height=""100""/><p style=""color:#b0f442"">DIVISION: <!-- -->DIAMOND</p></div>
<div class=""flex items-center gap-2 text-3xl""><img src=""https://flagcdn.com/16x12/us.png"" width=""16"" height=""12"" alt=""US"" class=""inline-block""/>owen &amp; co</div><p class=""text-base text-zinc-400"">Retro Rewind CT Season 2</p>
<p><span style=""color:#4286f4"">Rank</span>: <!-- -->1<!-- --> / <!-- -->129</p><p><span style=""color:#4286f4"">Percentile</span>: <!-- -->99<!-- -->th</p>
<p><span style=""color:#4286f4"">Peak</span>: <!-- -->6590<!-- --> <span style=""color:#4286f4"">Lowest</span>: <!-- -->2250</p>
<table><tbody><tr style=""background-color:#222""><td>Wins</td><td>Losses</td><td>Win %</td></tr><tr><td><span>28</span></td><td><span>10</span></td><td>73.7</td></tr></tbody></table>
<table><tbody><tr><td>Streak</td><td>W - L (10)</td></tr><tr><td><span>-2</span></td><td>7-3</td></tr></tbody></table>
<table><thead><tr><td>#</td><td>Date</td><td>Tier</td><td>Format</td><td>Races</td><td>Rank</td><td>Score</td><td>Result</td><td>+/-</td><td>MMR</td></tr></thead><tbody>
<tr style=""background-color:#4c7a01""><td>3</td><td><a href=""/mogis/x"">Oct 3, 2026, 9:17 PM</a></td><td>Tier Squad Queue</td><td>2v2</td><td>12</td><td>1st</td><td>119</td><td>Win</td><td>+163</td><td>6487</td></tr>
<tr style=""background-color:#3b3b3b""><td>2</td><td>Aug 22, 2026, 12:26 AM</td><td>-</td><td>Adjustment</td><td>-</td><td>-</td><td>-</td><td>-</td><td>+40</td><td>6324</td></tr>
<tr style=""background-color:#75291b""><td>1</td><td>Aug 9, 2026, 5:02 AM</td><td>Tier All</td><td>2v2</td><td>12</td><td>5th</td><td>70</td><td>Loss</td><td>-266</td><td>6284</td></tr>
</tbody></table></body></html>";

    [Fact]
    public void ThePage_GivesWhoTheyAre_AndWhereTheyRank()
    {
        var profile = RrLoungeProfiles.Parse(Page)!;

        Assert.Equal("owen & co", profile.Name);
        Assert.Equal("US", profile.CountryCode);
        Assert.Equal("Retro Rewind CT Season 2", profile.Season);
        Assert.Equal("Diamond", profile.Division);
        Assert.Equal(1, profile.Rank);
        Assert.Equal(129, profile.Players);
        Assert.Equal("99th", profile.Percentile);
        Assert.Equal(6590, profile.Peak);
        Assert.Equal(2250, profile.Lowest);
        Assert.Equal(6487, profile.Mmr);
    }

    [Fact]
    public void ThePage_GivesTheStats_InTheSitesOrder()
    {
        var profile = RrLoungeProfiles.Parse(Page)!;

        Assert.Equal(["Wins", "Losses", "Win %", "Streak", "W - L (10)"], profile.Stats.Select(stat => stat.Label));
        Assert.Equal("28", profile.Stat("wins"));
        Assert.Equal("-2", profile.Stat("Streak"));
    }

    [Fact]
    public void ThePage_GivesEveryMogi_NewestFirst_WithAdjustments()
    {
        var history = RrLoungeProfiles.Parse(Page)!.History;

        Assert.Equal([3, 2, 1], history.Select(mogi => mogi.Number));
        Assert.True(history[0].IsWin);
        Assert.Equal("Oct 3, 2026, 9:17 PM", history[0].Date);
        Assert.Equal("Tier Squad Queue", history[0].Tier);
        Assert.True(history[1].IsAdjustment);
        Assert.True(history[2].IsLoss);
        Assert.Equal("-266", history[2].Change);
        Assert.Equal(6284, history[2].Mmr);
    }

    [Fact]
    public void APageThatIsNotAPlayer_GivesNull()
    {
        Assert.Null(RrLoungeProfiles.Parse(null));
        Assert.Null(RrLoungeProfiles.Parse("<html><body><p>Loading player profile…</p></body></html>"));
    }

    [Theory]
    [InlineData("/icons/new_diamond.png", "https://rr-lounge.com/icons/new_diamond.png")]
    [InlineData("https://evil.example/x.png", null)]
    [InlineData("/icons/../secret", null)]
    [InlineData(null, null)]
    public void RankIcons_OnlyComeFromTheSite(string? icon, string? url) =>
        Assert.Equal(url, RrLoungeData.IconUrl(new RrLoungeTier { Name = "Diamond", Icon = icon }));
}
