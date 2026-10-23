using WheelWizard.Views.Shell;

namespace WheelWizard.Test.Features;

public class SidebarLayoutModelTests
{
    private static readonly string[] BuiltIn = ["home", "profiles", "patches", "miis", "themes", "rooms", "leaderboard", "friends"];
    private const int General = 5;

    private static SidebarLayoutModel Parse(string? json = null, params string[] pins) =>
        SidebarLayoutModel.Parse(json, BuiltIn, General, pins);

    private static string Default(int index) =>
        index == 0 ? "General"
        : index == 1 ? "Online"
        : "New category";

    private static string Items(SidebarLayoutModel model, int section) => string.Join(",", model.Sections[section].Items);

    [Fact]
    public void NoSavedLayout_GivesTheDefaultSidebar()
    {
        var model = Parse();

        Assert.Equal(2, model.Sections.Count);
        Assert.Equal("home,profiles,patches,miis,themes", Items(model, 0));
        Assert.Equal("rooms,leaderboard,friends", Items(model, 1));
        Assert.Empty(model.Hidden);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"Sections\": 5}")]
    public void DamagedSavedLayout_FallsBackToTheDefault(string json)
    {
        var model = Parse(json);

        Assert.Equal("home,profiles,patches,miis,themes", Items(model, 0));
        Assert.Equal("rooms,leaderboard,friends", Items(model, 1));
    }

    [Fact]
    public void PinnedTabs_JoinTheSecondCategory_AndKeepTheirOrder()
    {
        var model = Parse(null, "pin:Ghosts", "pin:Backup");

        Assert.Equal("rooms,leaderboard,friends,pin:Ghosts,pin:Backup", Items(model, 1));
    }

    [Fact]
    public void SavedLayout_IsKept_AndRepaired()
    {
        // Unknown and repeated items are dropped, missing built-in items return to their default category, and a pin that
        // was removed disappears.
        const string json =
            """{"Sections":[{"Name":"Mine","Items":["rooms","home","gone","home","pin:Old"]},{"Name":"","Items":["themes"]}],"Hidden":["rooms","gone"]}""";

        var model = Parse(json, "pin:Ghosts");

        Assert.Equal("Mine", model.Sections[0].Name);
        Assert.Equal("rooms,home,profiles,patches,miis", Items(model, 0));
        Assert.Equal("themes,leaderboard,friends,pin:Ghosts", Items(model, 1));
        Assert.Equal(["rooms"], model.Hidden);
    }

    [Fact]
    public void ToJson_RoundTrips()
    {
        var model = Parse();
        model.AddSection();
        model.Sections[2].Name = "Extras";
        model.MoveItem(1, 0, 2, 0);
        model.ToggleHidden("home");

        var again = Parse(model.ToJson());

        Assert.Equal(3, again.Sections.Count);
        Assert.Equal("Extras", again.Sections[2].Name);
        Assert.Equal("rooms", Items(again, 2));
        Assert.Equal("leaderboard,friends", Items(again, 1));
        Assert.True(again.IsHidden("home"));
    }

    [Fact]
    public void MoveItem_WithinACategory_DownAndUp()
    {
        var model = Parse();

        // Drop "home" in the gap under "patches" (gaps count from 0 above the first item).
        model.MoveItem(0, 0, 0, 3);
        Assert.Equal("profiles,patches,home,miis,themes", Items(model, 0));

        // And "themes" back to the very top.
        model.MoveItem(0, 4, 0, 0);
        Assert.Equal("themes,profiles,patches,home,miis", Items(model, 0));
    }

    [Fact]
    public void MoveItem_DroppedWhereItIsAlready_ChangesNothing()
    {
        var model = Parse();

        model.MoveItem(0, 2, 0, 2);
        model.MoveItem(0, 2, 0, 3);

        Assert.Equal("home,profiles,patches,miis,themes", Items(model, 0));
    }

    [Fact]
    public void MoveItem_BetweenCategories_IncludingAnEmptyOne()
    {
        var model = Parse();
        model.AddSection();

        model.MoveItem(1, 1, 0, 5); // "leaderboard" to the end of the first category
        model.MoveItem(0, 0, 2, 0); // "home" into the new, empty category

        Assert.Equal("profiles,patches,miis,themes,leaderboard", Items(model, 0));
        Assert.Equal("rooms,friends", Items(model, 1));
        Assert.Equal("home", Items(model, 2));
    }

    [Fact]
    public void MoveItem_WithAnOutOfRangeDrop_IsClamped_AndBadInputIsIgnored()
    {
        var model = Parse();

        model.MoveItem(0, 0, 1, 99);
        model.MoveItem(5, 0, 0, 0);
        model.MoveItem(0, 99, 0, 0);

        Assert.Equal("profiles,patches,miis,themes", Items(model, 0));
        Assert.Equal("rooms,leaderboard,friends,home", Items(model, 1));
    }

    [Fact]
    public void MoveSection_KeepsTheDefaultNames_ThatFollowedThePlace()
    {
        var model = Parse();

        model.MoveSection(1, 0, Default);

        Assert.Equal("Online", model.Sections[0].Name);
        Assert.Equal("General", model.Sections[1].Name);
        Assert.Equal("rooms,leaderboard,friends", Items(model, 0));
        Assert.Equal("home,profiles,patches,miis,themes", Items(model, 1));
    }

    [Fact]
    public void MoveSection_KeepsNamesTheUserTyped_AndWorksWithThree()
    {
        var model = Parse();
        model.AddSection();
        model.Sections[2].Name = "Mine";

        model.MoveSection(2, 1, Default);

        Assert.Equal(["General", "Mine", "Online"], model.Sections.Select(section => section.Name));
    }

    [Fact]
    public void AddSection_AppendsAnEmptyCategory()
    {
        var model = Parse();

        model.AddSection();

        Assert.Equal(3, model.Sections.Count);
        Assert.Empty(model.Sections[2].Items);
        Assert.Equal("", model.Sections[2].Name);
    }

    [Fact]
    public void RemoveSection_MovesItsItemsUp_AndNeverRemovesTheFirst()
    {
        var model = Parse();
        model.AddSection();
        model.MoveItem(0, 0, 2, 0);

        model.RemoveSection(2);
        Assert.Equal(2, model.Sections.Count);
        Assert.Equal("rooms,leaderboard,friends,home", Items(model, 1));

        model.RemoveSection(0);
        model.RemoveSection(7);
        Assert.Equal(2, model.Sections.Count);
    }

    [Fact]
    public void ToggleHidden_HidesAndRestores()
    {
        var model = Parse();

        model.ToggleHidden("themes");
        Assert.True(model.IsHidden("themes"));
        Assert.False(model.IsHidden("home"));

        model.ToggleHidden("themes");
        Assert.False(model.IsHidden("themes"));
        Assert.Empty(model.Hidden);
    }

    [Fact]
    public void Reset_IsJustAnEmptySavedLayout()
    {
        var model = Parse();
        model.AddSection();
        model.MoveItem(0, 0, 2, 0);
        model.ToggleHidden("rooms");

        var reset = Parse("");

        Assert.Equal(2, reset.Sections.Count);
        Assert.Equal("home,profiles,patches,miis,themes", Items(reset, 0));
        Assert.Empty(reset.Hidden);
    }

    private static readonly string[] WithCompetitive = [.. BuiltIn, "competitive-lounge", "competitive-ghosts"];
    private static readonly Dictionary<string, string> Competitive = new()
    {
        ["competitive-lounge"] = "Competitive",
        ["competitive-ghosts"] = "Competitive",
    };

    private static SidebarLayoutModel ParseCompetitive(string? json = null) =>
        SidebarLayoutModel.Parse(json, WithCompetitive, General, [], Competitive);

    [Fact]
    public void TabsWithACategoryOfTheirOwn_GetIt_ByDefault()
    {
        var model = ParseCompetitive();

        Assert.Equal(3, model.Sections.Count);
        Assert.Equal("rooms,leaderboard,friends", Items(model, 1));
        Assert.Equal("Competitive", model.Sections[2].Name);
        Assert.Equal("competitive-lounge,competitive-ghosts", Items(model, 2));
    }

    [Fact]
    public void ASavedSidebarFromBefore_GainsTheCategory_AfterItsOwnOnes()
    {
        // Saved before the category existed, with RR Lounge and Ghosts under their old names, and a category of the user's.
        const string json =
            """{"Sections":[{"Name":"","Items":["home"]},{"Name":"","Items":["rooms","lounge","ghosts"]},{"Name":"Mine","Items":["themes"]}],"Hidden":[]}""";

        var model = ParseCompetitive(json);

        Assert.Equal(4, model.Sections.Count);
        Assert.Equal("Mine", model.Sections[2].Name);
        Assert.Equal("Competitive", model.Sections[3].Name);
        Assert.Equal("competitive-lounge,competitive-ghosts", Items(model, 3));
    }

    [Fact]
    public void TheCategory_IsFoundByName_WhereverTheUserMovedIt()
    {
        const string json =
            """{"Sections":[{"Name":"competitive ","Items":["competitive-lounge"]},{"Name":"","Items":["home"]},{"Name":"","Items":["rooms"]}],"Hidden":[]}""";

        var model = ParseCompetitive(json);

        // No second category is added: Ghosts joins RR Lounge in the renamed, moved one.
        Assert.Equal(3, model.Sections.Count);
        Assert.Equal("competitive-lounge", model.Sections[0].Items[0]);
        Assert.Contains("competitive-ghosts", model.Sections[0].Items);
    }
}
