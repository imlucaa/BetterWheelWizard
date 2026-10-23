using WheelWizard.RrLounge;

namespace WheelWizard.Test.Features.RrLounge;

public class RrLoungeDataTests
{
    // A cut-down copy of rr-lounge.com/leaderboard: the data sits in the Next.js script as escaped JSON.
    private const string Page = """
        <script>self.__next_f.push([1,"[\"$\",\"$L2\",\"a\",{\"href\":\"/leaderboard?category=rr&season=retro-rewind-season-7\",\"className\":\"px-3 bg-[#53a5ed]\",\"children\":\"Retro Rewind Season 7\"}],[\"$\",\"$L2\",\"b\",{\"href\":\"/leaderboard?category=rr&season=retro-rewind-season-6\",\"className\":\"px-3\",\"children\":\"Retro Rewind Season 6\"}]]}],[\"$\",\"$L18\",null,{\"players\":[{\"id\":\"b\",\"name\":\"Lumis [x]\",\"countryCode\":\"US\",\"mmr\":9501,\"peakMmr\":9670,\"mogiCount\":112,\"winPercentage\":70.3,\"last10Wins\":7,\"last10Losses\":3,\"averageScore\":93.5,\"averageScoreLast10\":92.5,\"partnerAverageScore\":71.1},{\"id\":\"a\",\"name\":\"intz\",\"countryCode\":\"DE\",\"mmr\":13316,\"peakMmr\":13316,\"mogiCount\":66,\"winPercentage\":81.5,\"last10Wins\":8,\"last10Losses\":2,\"averageScore\":null,\"averageScoreLast10\":119.4,\"partnerAverageScore\":67.3}],\"rankTiers\":[{\"name\":\"Master\",\"minMmr\":9000,\"color\":\"#cfe2f3\"},{\"name\":\"Opal\",\"minMmr\":-9999,\"color\":\"#ffc6ff\"},{\"name\":\"Legend\",\"minMmr\":11000,\"color\":\"#470000\"}],\"seasonParam\":\"retro-rewind-season-7\",\"categoryParam\":\"rr\"}]]}]\n"])</script>
        """;

    [Fact]
    public void ThePage_GivesEveryPlayersStats_RankedByMmr()
    {
        var players = RrLoungeData.Ranked(RrLoungeData.ParseStats(Page));

        Assert.Equal(["intz", "Lumis [x]"], players.Select(player => player.Name));
        Assert.Equal([1, 2], players.Select(player => player.Rank));
        Assert.Equal(112, players[1].MogiCount);
        Assert.Equal("US", players[1].CountryCode);
        Assert.Null(players[0].AverageScore);
        Assert.Equal("-", RrLoungeData.Number(players[0].AverageScore));
        Assert.Equal("70.3", RrLoungeData.Number(players[1].WinPercentage));
    }

    [Fact]
    public void ThePage_GivesTheSeasons_AndTheRankColours()
    {
        Assert.Equal(
            [
                new RrLoungeSeasonLink("retro-rewind-season-7", "Retro Rewind Season 7"),
                new("retro-rewind-season-6", "Retro Rewind Season 6"),
            ],
            RrLoungeData.ParseSeasons(Page)
        );
        Assert.Equal("retro-rewind-season-7", RrLoungeData.ParseSeason(Page));

        var tiers = RrLoungeData.ParseTiers(Page);
        Assert.Equal(["Opal", "Master", "Legend"], tiers.Select(tier => tier.Name));
        Assert.Equal("Legend", RrLoungeData.TierFor(13316, tiers)!.Name);
        Assert.Equal("Master", RrLoungeData.TierFor(9000, tiers)!.Name);
        Assert.Equal("Opal", RrLoungeData.TierFor(-50000, tiers)!.Name);
    }

    [Fact]
    public void APageThatChangedShape_GivesNothing_InsteadOfThrowing()
    {
        Assert.Empty(RrLoungeData.ParseStats("<html>new site</html>"));
        Assert.Empty(RrLoungeData.ParseStats("\"players\":[{\"id\":\"broken"));
        Assert.Empty(RrLoungeData.ParseTiers(null));
        Assert.Null(RrLoungeData.ParseSeason(""));
    }

    [Fact]
    public void Search_MatchesPartOfTheName_IgnoringCase()
    {
        var players = RrLoungeData.ParseStats(Page);

        Assert.Equal(["Lumis [x]"], RrLoungeData.Search(players, " LUM ").Select(player => player.Name));
        Assert.Equal(2, RrLoungeData.Search(players, "").Count());
    }

    [Theory]
    [InlineData("Lumis", "https://rr-lounge.com/players/lumis?category=rr&season=retro-rewind-season-7")]
    [InlineData("Zoë & Co!", "https://rr-lounge.com/players/zoe-and-co?category=rr&season=retro-rewind-season-7")]
    [InlineData("★", "https://rr-lounge.com/players/player?category=rr&season=retro-rewind-season-7")]
    public void PlayerUrl_IsTheSameLinkAsTheSite(string name, string url) =>
        Assert.Equal(url, RrLoungeData.PlayerUrl(name, RrLoungeLadder.Rr, "retro-rewind-season-7"));

    [Fact]
    public void FlagUrl_NeedsATwoLetterCode()
    {
        Assert.Equal("https://flagcdn.com/16x12/gb.png", RrLoungeData.FlagUrl("GB"));
        Assert.Null(RrLoungeData.FlagUrl(null));
        Assert.Null(RrLoungeData.FlagUrl("G1"));
    }

    [Theory]
    [InlineData("Retro Rewind Season 7", "Season 7")]
    [InlineData("Retro Rewind CT Season 2", "CT Season 2")]
    [InlineData("Some Other Cup", "Some Other Cup")]
    public void SeasonNames_DropTheRetroRewindPrefix(string name, string shortName) =>
        Assert.Equal(shortName, RrLoungeData.ShortSeasonName(name));

    [Fact]
    public void Changes_AreWhoseMmrMoved_AndWhoIsNew()
    {
        RrLoungePlayer Player(string id, int mmr) =>
            new()
            {
                Id = id,
                Name = id,
                Mmr = mmr,
            };

        var changes = RrLoungeData.Changes(
            [Player("a", 1000), Player("b", 2000), Player("c", 3000)],
            [Player("a", 1000), Player("b", 2163), Player("c", 2900), Player("d", 500)]
        );

        Assert.Equal(
            new Dictionary<string, int?>
            {
                ["b"] = 163,
                ["c"] = -100,
                ["d"] = null,
            },
            changes
        );
    }
}
