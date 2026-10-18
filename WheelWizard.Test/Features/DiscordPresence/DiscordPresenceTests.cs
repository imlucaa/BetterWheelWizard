using WheelWizard.DiscordPresence;

namespace WheelWizard.Test.Features.DiscordPresence;

public class DiscordPresenceTests
{
    [Theory]
    [InlineData(30, "In a Grand Prix")]
    [InlineData(31, "In Time Trials")]
    [InlineData(85, "In a WiFi menu")]
    [InlineData(88, "Voting for a WiFi VS")]
    [InlineData(104, "Racing in a WiFi VS")]
    [InlineData(106, "Spectating a WiFi VS")]
    [InlineData(119, "Racing in a WiFi Friend Coin Runners")]
    public void Describe_UsesTheGamesOwnWording(int sectionId, string expected) =>
        Assert.Equal(expected, RetroRewindSections.Describe(sectionId));

    [Theory]
    [InlineData(72)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500)]
    public void Describe_FallsBackToMenuForAnyOtherScreen(int sectionId) =>
        Assert.Equal("In a Menu", RetroRewindSections.Describe(sectionId));

    [Theory]
    [InlineData(84, false)]
    [InlineData(85, true)]
    [InlineData(119, true)]
    [InlineData(120, false)]
    public void IsOnline_CoversOnlyTheWifiScreens(int sectionId, bool expected) =>
        Assert.Equal(expected, RetroRewindSections.IsOnline(sectionId));

    [Theory]
    [InlineData(85, false)] // WiFi menu
    [InlineData(88, false)] // voting
    [InlineData(103, false)] // voting in a friend room
    [InlineData(104, true)] // racing
    [InlineData(111, true)] // spectating
    [InlineData(119, true)]
    [InlineData(120, false)]
    public void IsOnlineRace_ExcludesMenusAndVoting(int sectionId, bool expected) =>
        Assert.Equal(expected, RetroRewindSections.IsOnlineRace(sectionId));

    [Theory]
    [InlineData("1561280163868704799", true)]
    [InlineData("123456789012345", true)]
    [InlineData("12345678901234", false)]
    [InlineData("123456789012345678901", false)]
    [InlineData("15612801638687047x9", false)]
    [InlineData("", false)]
    public void IsValidApplicationId_AcceptsOnlyDiscordSizedNumbers(string id, bool expected) =>
        Assert.Equal(expected, DiscordPresenceService.IsValidApplicationId(id));

    [Fact]
    public void RequiredName_IsRetroRewind() => Assert.Equal("Retro Rewind", DiscordPresenceService.RequiredName);
}
