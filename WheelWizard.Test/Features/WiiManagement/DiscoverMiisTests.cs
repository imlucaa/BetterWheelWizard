using WheelWizard.WiiManagement.MiiManagement.Views;

namespace WheelWizard.Test.Features.WiiManagement;

public class DiscoverMiisTests
{
    [Fact]
    public void RandomPages_AreDifferentPages_FromAllOverTheLeaderboard()
    {
        var random = new Random(7);
        var seen = new HashSet<int>();
        for (var i = 0; i < 200; i++)
        {
            var pages = MiiListPage.RandomPages(2635, 3, random);
            Assert.Equal(3, pages.Distinct().Count());
            Assert.All(pages, page => Assert.InRange(page, 1, 2635));
            seen.UnionWith(pages);
        }

        // Not just the top of the board.
        Assert.Contains(seen, page => page > 1000);
    }

    [Fact]
    public void RandomPages_OnASmallBoard_TakeWhatThereIs()
    {
        Assert.Equal([1], MiiListPage.RandomPages(1, 3, new Random(1)));
        Assert.Equal([1, 2], MiiListPage.RandomPages(2, 3, new Random(1)).Order());
        Assert.Equal([1], MiiListPage.RandomPages(0, 3, new Random(1)));
    }
}
