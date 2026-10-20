using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.Test.Features.WiiManagement;

public class SessionStatsTests
{
    private const string Code = "1111-2222-3333";

    [Fact]
    public void FirstSighting_IsTheStartingPoint_SoNothingHasChangedYet()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(Code, 100, 40, 5000);

        Assert.Equal(new SessionStats(0, 0, 0), calculator.For(Code, 100, 40, 5000));
    }

    [Fact]
    public void LaterNumbers_AreCountedFromTheStart()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(Code, 100, 40, 5000);

        Assert.Equal(new SessionStats(7, 3, 120), calculator.For(Code, 107, 43, 5120));
    }

    [Fact]
    public void LosingVr_IsANegativeChange()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(Code, 10, 1, 5000);

        Assert.Equal(-35, calculator.For(Code, 11, 1, 4965)!.VrChange);
    }

    [Fact]
    public void ObservingAgain_DoesNotMoveTheStartingPoint()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(Code, 100, 40, 5000);
        calculator.Observe(Code, 105, 42, 5060);

        Assert.Equal(5, calculator.For(Code, 105, 42, 5060)!.Races);
    }

    [Fact]
    public void ASaveThatWentBackwards_StartsOverInsteadOfShowingNegativeRaces()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(Code, 100, 40, 5000);
        calculator.Observe(Code, 60, 20, 4200);

        Assert.Equal(new SessionStats(0, 0, 0), calculator.For(Code, 60, 20, 4200));
        Assert.Equal(new SessionStats(2, 1, 50), calculator.For(Code, 62, 21, 4250));
    }

    [Fact]
    public void Races_NeverGoBelowZero_EvenWithoutAFreshObserve()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(Code, 100, 40, 5000);

        var stats = calculator.For(Code, 90, 30, 5000)!;

        Assert.Equal((0, 0), (stats.Races, stats.Wins));
    }

    [Fact]
    public void EachLicense_HasItsOwnCount()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe("1111-2222-3333", 10, 5, 5000);
        calculator.Observe("4444-5555-6666", 200, 90, 9000);

        Assert.Equal(new SessionStats(2, 1, 30), calculator.For("1111-2222-3333", 12, 6, 5030));
        Assert.Equal(new SessionStats(0, 0, 0), calculator.For("4444-5555-6666", 200, 90, 9000));
    }

    [Fact]
    public void FriendCodesMatchWithOrWithoutDashes()
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe("111122223333", 10, 5, 5000);

        Assert.Equal(1, calculator.For("1111-2222-3333", 11, 5, 5000)!.Races);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000-0000-0000")]
    public void LicensesWithoutAFriendCode_AreIgnored(string? code)
    {
        var calculator = new SessionStatsCalculator();
        calculator.Observe(code, 10, 5, 5000);

        Assert.Null(calculator.For(code, 12, 5, 5000));
    }

    [Fact]
    public void AnUnseenLicense_HasNoStats() => Assert.Null(new SessionStatsCalculator().For(Code, 1, 1, 1));
}

public class SessionStatsTrackerTests
{
    private static readonly DateTime Written = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static WheelWizard.WiiManagement.GameLicense.Domain.LicenseProfile License(string code, uint races, uint wins, uint vr) =>
        new()
        {
            FriendCode = code,
            Vr = vr,
            Br = 5000,
            RegionId = 1,
            Mii = null,
            TotalRaceCount = races,
            TotalWinCount = wins,
        };

    private static (SessionStatsTracker Tracker, IGameLicenseSingletonService Licenses) Create(
        WheelWizard.WiiManagement.GameLicense.Domain.LicenseProfile user
    )
    {
        var licenses = Substitute.For<IGameLicenseSingletonService>();
        var collection = new WheelWizard.WiiManagement.GameLicense.Domain.LicenseCollection { Users = [user] };
        licenses.LicenseCollection.Returns(collection);
        licenses.ActiveUser.Returns(_ => collection.Users[0]);
        licenses.GetSaveWriteTimeUtc().Returns(Written);
        var tracker = new SessionStatsTracker(
            licenses,
            Substitute.For<WheelWizard.Shared.Polling.IPollingScheduler>(),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionStatsTracker>.Instance
        );
        return (tracker, licenses);
    }

    [Fact]
    public void FirstCheck_ShowsZeroes_WithoutReadingTheSaveAgain()
    {
        var (tracker, licenses) = Create(License("1111-2222-3333", 100, 40, 5000));

        tracker.Refresh();

        Assert.Equal(new SessionStats(0, 0, 0), tracker.Current);
        licenses.DidNotReceive().LoadLicense();
    }

    [Fact]
    public void WhenTheSaveIsWritten_ItIsReadAgain_AndTheChangeShows()
    {
        var (tracker, licenses) = Create(License("1111-2222-3333", 100, 40, 5000));
        var changes = 0;
        tracker.Changed += () => changes++;
        tracker.Refresh();

        licenses.GetSaveWriteTimeUtc().Returns(Written.AddMinutes(2));
        licenses.When(l => l.LoadLicense()).Do(_ => licenses.LicenseCollection.Users[0] = License("1111-2222-3333", 103, 41, 5075));
        tracker.Refresh();

        Assert.Equal(new SessionStats(3, 1, 75), tracker.Current);
        licenses.Received(1).LoadLicense();
        licenses.Received(1).RefreshOnlineStatus();
        Assert.Equal(2, changes);
    }

    [Fact]
    public void WhenTheSaveIsUnchanged_NothingIsReadOrAnnounced()
    {
        var (tracker, licenses) = Create(License("1111-2222-3333", 10, 5, 5000));
        tracker.Refresh();
        var changes = 0;
        tracker.Changed += () => changes++;

        tracker.Refresh();
        tracker.Refresh();

        Assert.Equal(0, changes);
        licenses.DidNotReceive().LoadLicense();
    }

    [Fact]
    public void ASaveThatAppearsLater_IsReadAndCountsFromThere()
    {
        var (tracker, licenses) = Create(License("0000-0000-0000", 0, 0, 5000));
        licenses.GetSaveWriteTimeUtc().Returns((DateTime?)null);
        tracker.Refresh();
        Assert.Null(tracker.Current);

        licenses.GetSaveWriteTimeUtc().Returns(Written);
        licenses.When(l => l.LoadLicense()).Do(_ => licenses.LicenseCollection.Users[0] = License("1111-2222-3333", 50, 20, 5200));
        tracker.Refresh();

        Assert.Equal(new SessionStats(0, 0, 0), tracker.Current);
    }

    [Fact]
    public void AnEmptyLicense_HasNoStats()
    {
        var (tracker, _) = Create(License("0000-0000-0000", 0, 0, 5000));

        tracker.Refresh();

        Assert.Null(tracker.Current);
    }

    [Fact]
    public void WithNoLicenseLoadedYet_ItWaitsInsteadOfThrowing()
    {
        var (tracker, licenses) = Create(License("1111-2222-3333", 1, 1, 5000));
        licenses.ActiveUser.Returns(_ => throw new ArgumentOutOfRangeException("index"));

        tracker.Refresh();

        Assert.Null(tracker.Current);
    }
}

public class SessionStatsActivityTests
{
    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 1, true)]
    [InlineData(0, 0, -20, true)]
    [InlineData(1, 0, 0, true)]
    [InlineData(5, 2, 120, true)]
    public void HasActivity_IsTrueOnlyOnceSomethingMoved(int races, int wins, int vr, bool expected) =>
        Assert.Equal(expected, new SessionStats(races, wins, vr).HasActivity);
}
