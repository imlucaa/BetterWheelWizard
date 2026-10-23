using WheelWizard.Models.RRInfo;
using WheelWizard.RrRooms;

namespace WheelWizard.Test.Features.RrRooms;

public class PlayerFlagsTests
{
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void NothingSaved_GivesNoFlags(string? json) => Assert.Empty(PlayerFlags.Parse(json));

    [Fact]
    public void Flagging_IsStoredByFriendCode_WithOrWithoutDashes()
    {
        var json = PlayerFlags.With(null, "1111-2222-3333", "  Wii off  ");

        var flags = PlayerFlags.Parse(json);

        Assert.Equal("Wii off", PlayerFlags.NoteFor(flags, "111122223333"));
        Assert.Equal("Wii off", PlayerFlags.NoteFor(flags, "1111-2222-3333"));
        Assert.Equal(string.Empty, PlayerFlags.NoteFor(flags, "9999-9999-9999"));
    }

    [Fact]
    public void AnEmptyNote_RemovesTheFlag_AndABadCodeIsIgnored()
    {
        var json = PlayerFlags.With(null, "1111-2222-3333", "VR farmer");
        json = PlayerFlags.With(json, "1111-2222-3333", "  ");
        json = PlayerFlags.With(json, "12-34", "Wii off");

        Assert.Empty(PlayerFlags.Parse(json));
    }

    [Fact]
    public void ALongNote_IsShortened()
    {
        var flags = PlayerFlags.Parse(PlayerFlags.With(null, "1111-2222-3333", new string('x', 60)));

        Assert.Equal(PlayerFlags.MaxNoteLength, PlayerFlags.NoteFor(flags, "111122223333").Length);
    }

    [Fact]
    public void Apply_MarksPlayersAndSumsUpEachRoom()
    {
        var flags = PlayerFlags.Parse(PlayerFlags.With(PlayerFlags.With(null, "1111-2222-3333", "Wii off"), "4444-5555-6666", "VR farmer"));
        var one = Room(("Ann", "1111-2222-3333"), ("Bo", "7777-8888-9999"));
        var two = Room(("Ann", "1111-2222-3333"), ("Cy", "4444-5555-6666"));
        var clean = Room(("Bo", "7777-8888-9999"));

        PlayerFlags.Apply([one, two, clean], flags);

        Assert.True(one.Players[0].IsFlagged);
        Assert.False(one.Players[1].IsFlagged);
        Assert.Equal("Wii off", one.FlagSummary);
        Assert.Equal("2 flagged", two.FlagSummary);
        Assert.False(clean.HasFlags);
    }

    [Fact]
    public void NoteChoices_AreTheQuickNotes_ThenYourOwn_WithoutRepeats()
    {
        var flags = PlayerFlags.Parse(
            PlayerFlags.With(PlayerFlags.With(null, "1111-2222-3333", "wii off"), "4444-5555-6666", "Lag switch")
        );

        var choices = PlayerFlags.NoteChoices(flags);

        Assert.Equal(["Wii off", "VR farmer", "Troll", "Cheater", "Lag switch"], choices);
    }

    [Fact]
    public void ANewFlag_IsRed_AndChangingTheNote_KeepsTheColourYouPicked()
    {
        var json = PlayerFlags.With(null, "1111-2222-3333", "Wii off");
        Assert.Equal(PlayerFlags.DefaultColor, PlayerFlags.FlagFor(PlayerFlags.Parse(json), "111122223333")!.Color);

        json = PlayerFlags.WithColor(json, "1111-2222-3333", "#60A5FA");
        json = PlayerFlags.With(json, "1111-2222-3333", "Troll");

        Assert.Equal(new PlayerFlag("Troll", "#60A5FA"), PlayerFlags.FlagFor(PlayerFlags.Parse(json), "111122223333"));
    }

    [Fact]
    public void AColourForSomeoneNotFlagged_OrNotOnTheList_IsIgnored()
    {
        var json = PlayerFlags.WithColor(null, "1111-2222-3333", "#60A5FA");
        Assert.Empty(PlayerFlags.Parse(json));

        json = PlayerFlags.WithColor(PlayerFlags.With(null, "1111-2222-3333", "Wii off"), "1111-2222-3333", "#123456");
        Assert.Equal(PlayerFlags.DefaultColor, PlayerFlags.FlagFor(PlayerFlags.Parse(json), "111122223333")!.Color);
    }

    [Fact]
    public void FlagsSavedBeforeColours_StillLoad_AsRed()
    {
        var flags = PlayerFlags.Parse("""{"111122223333":"Wii off"}""");

        Assert.Equal(new PlayerFlag("Wii off", PlayerFlags.DefaultColor), PlayerFlags.FlagFor(flags, "1111-2222-3333"));
    }

    [Fact]
    public void Apply_GivesTheRoomTheColourOfItsFirstFlaggedPlayer()
    {
        var json = PlayerFlags.WithColor(PlayerFlags.With(null, "1111-2222-3333", "Wii off"), "1111-2222-3333", "#4ADE80");
        var room = Room(("Bo", "7777-8888-9999"), ("Ann", "1111-2222-3333"));

        PlayerFlags.Apply([room], PlayerFlags.Parse(json));

        Assert.Equal("#4ADE80", room.FlagColor);
        Assert.Equal("#4ADE80", room.Players[1].FlagColor);
    }

    [Fact]
    public void Apply_ClearsAFlagThatWasRemoved()
    {
        var room = Room(("Ann", "1111-2222-3333"));
        PlayerFlags.Apply([room], PlayerFlags.Parse(PlayerFlags.With(null, "1111-2222-3333", "Wii off")));

        PlayerFlags.Apply([room], PlayerFlags.Parse(null));

        Assert.False(room.Players[0].IsFlagged);
        Assert.False(room.HasFlags);
    }
}
