using WheelWizard.RrRooms;
using WheelWizard.Views.Pages;

namespace WheelWizard.Test.Features;

public class LeaderboardPageTests
{
    [Fact]
    public void ResolveRank_AcceptsPositiveRanksAboveLegacyLimit()
    {
        var entry = new RwfcLeaderboardEntry { Pid = "1", Rank = 64965 };

        Assert.Equal(64965, LeaderboardPage.ResolveRank(entry, 6));
    }

    [Fact]
    public void ResolveRank_FallsBackToActiveRankThenResultPosition()
    {
        Assert.Equal(51002, LeaderboardPage.ResolveRank(new RwfcLeaderboardEntry { Pid = "1", ActiveRank = 51002 }, 3));
        Assert.Equal(4, LeaderboardPage.ResolveRank(new RwfcLeaderboardEntry { Pid = "1" }, 3));
    }

    [Theory]
    [InlineData(1, "st")]
    [InlineData(2, "nd")]
    [InlineData(3, "rd")]
    [InlineData(4, "th")]
    [InlineData(11, "th")]
    [InlineData(12, "th")]
    [InlineData(13, "th")]
    [InlineData(21, "st")]
    [InlineData(52, "nd")]
    [InlineData(53, "rd")]
    public void GetOrdinalSuffix_FormatsLeaderboardPlacements(int rank, string expected)
    {
        Assert.Equal(expected, LeaderboardPage.GetOrdinalSuffix(rank));
    }
}
