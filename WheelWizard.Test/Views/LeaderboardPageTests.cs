using WheelWizard.RrRooms;
using WheelWizard.Views.Pages;

namespace WheelWizard.Test.Views;

public class LeaderboardPageTests
{
    [Theory]
    [InlineData(8, 3, 8)]
    [InlineData(null, 4, 4)]
    [InlineData(0, 0, 6)]
    public void ResolveRank_PrefersServerRanks_ThenFallsBackToPosition(int? rank, int? activeRank, int expected)
    {
        var entry = new RwfcLeaderboardEntry
        {
            Pid = "player",
            Rank = rank,
            ActiveRank = activeRank,
        };

        Assert.Equal(expected, LeaderboardPage.ResolveRank(entry, 5));
    }
}
