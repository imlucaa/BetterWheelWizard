using System.Text;
using WheelWizard.DiscordPresence;

namespace WheelWizard.Test.Features.DiscordPresence;

public class TrackNameBlockTests
{
    private static byte[] Block(string magic, string name)
    {
        var block = new byte[12 + 64];
        Encoding.ASCII.GetBytes(magic).CopyTo(block, 0);
        block[7] = 1;
        Encoding.ASCII.GetBytes(name).CopyTo(block, 12);
        return block;
    }

    [Fact]
    public void ReadsTheNameAfterTheHeader()
    {
        Assert.Equal("3DS Toad Circuit", DolphinMemoryReader.TrackNameFromBlock(Block("PULL", "3DS Toad Circuit")));
    }

    [Fact]
    public void ReturnsNull_WhenTheBlockIsNotThePacks()
    {
        Assert.Null(DolphinMemoryReader.TrackNameFromBlock(Block("NOPE", "3DS Toad Circuit")));
    }

    [Fact]
    public void ReturnsNull_WhenThereIsNoName()
    {
        Assert.Null(DolphinMemoryReader.TrackNameFromBlock(Block("PULL", "")));
    }

    [Fact]
    public void ReturnsNull_WhenTheNameIsNotPrintableText()
    {
        var block = Block("PULL", "ab");
        block[14] = 0x07;
        block[15] = 0x41;
        Assert.Null(DolphinMemoryReader.TrackNameFromBlock(block));
    }

    [Theory]
    [InlineData(31, true)] // Time Trials
    [InlineData(30, true)] // Grand Prix
    [InlineData(104, true)] // racing online
    [InlineData(88, false)] // voting online
    [InlineData(10, false)] // a menu
    public void ShowsGameTrack_OnlyWhereATrackIsInPlay(int section, bool expected)
    {
        Assert.Equal(expected, RetroRewindSections.ShowsGameTrack(section));
    }
}
