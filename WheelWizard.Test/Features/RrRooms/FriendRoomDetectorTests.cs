using WheelWizard.Models.RRInfo;
using WheelWizard.RrRooms;

namespace WheelWizard.Test.Features.RrRooms;

public class FriendRoomDetectorTests
{
    private static readonly IReadOnlySet<string> Friends = new HashSet<string> { "111122223333", "444455556666" };

    private static RrRoom Room(params (string Name, string Code)[] players) =>
        new()
        {
            Id = "room",
            Created = DateTime.UtcNow,
            Type = "public",
            Suspend = false,
            Players = players
                .Select(p => new RrPlayer
                {
                    Pid = p.Code,
                    Name = p.Name,
                    FriendCode = p.Code,
                })
                .ToList(),
        };

    [Fact]
    public void FirstList_IsOnlyABaseline()
    {
        var detector = new FriendRoomDetector();

        Assert.Empty(detector.Update([Room(("Ann", "1111-2222-3333"))], Friends));
    }

    [Fact]
    public void FriendAppearingLater_IsReportedOnce()
    {
        var detector = new FriendRoomDetector();
        detector.Update([Room(("Stranger", "9999-9999-9999"))], Friends);

        var joined = detector.Update([Room(("Stranger", "9999-9999-9999"), ("Ann", "1111-2222-3333"))], Friends);
        var again = detector.Update([Room(("Ann", "1111-2222-3333"))], Friends);

        Assert.Equal(["Ann"], joined);
        Assert.Empty(again);
    }

    [Fact]
    public void FriendWhoLeavesAndComesBack_IsReportedAgain()
    {
        var detector = new FriendRoomDetector();
        detector.Update([Room(("Ann", "1111-2222-3333"))], Friends);
        detector.Update([], Friends);

        var joined = detector.Update([Room(("Ann", "1111-2222-3333"))], Friends);

        Assert.Equal(["Ann"], joined);
    }

    [Fact]
    public void SeveralFriendsAtOnce_AreAllReported()
    {
        var detector = new FriendRoomDetector();
        detector.Update([], Friends);

        var joined = detector.Update([Room(("Ann", "1111-2222-3333")), Room(("Bo", "4444-5555-6666"))], Friends);

        Assert.Equal(["Ann", "Bo"], joined.Order());
    }

    [Fact]
    public void StrangersAndBlankCodes_AreIgnored()
    {
        var detector = new FriendRoomDetector();
        detector.Update([], Friends);

        var joined = detector.Update([Room(("Stranger", "9999-9999-9999"), ("NoCode", ""))], Friends);

        Assert.Empty(joined);
    }

    [Fact]
    public void FriendCodesWithOrWithoutDashes_Match()
    {
        var detector = new FriendRoomDetector();
        detector.Update([], Friends);

        var joined = detector.Update([Room(("Ann", "111122223333"))], Friends);

        Assert.Equal(["Ann"], joined);
    }
}
