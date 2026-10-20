using System.Text;
using WheelWizard.GameDiscs;
using WheelWizard.Models.Enums;

namespace WheelWizard.Test.Features.GameDiscs;

public class RegionDiscsTests
{
    private static byte[] Head(string id, int offset, string? magic = null)
    {
        var head = new byte[0x300];
        if (magic is not null)
            Encoding.ASCII.GetBytes(magic).CopyTo(head, 0);
        Encoding.ASCII.GetBytes(id).CopyTo(head, offset);
        return head;
    }

    [Theory]
    [InlineData("RMCE01", MarioKartWiiEnums.Regions.America)]
    [InlineData("RMCP01", MarioKartWiiEnums.Regions.Europe)]
    [InlineData("RMCJ01", MarioKartWiiEnums.Regions.Japan)]
    [InlineData("RMCK01", MarioKartWiiEnums.Regions.Korea)]
    public void DetectRegion_ReadsTheGameId_FromIso_Wbfs_AndRvz(string id, MarioKartWiiEnums.Regions expected)
    {
        Assert.Equal(expected, RegionDiscs.DetectRegion(Head(id, 0)));
        Assert.Equal(expected, RegionDiscs.DetectRegion(Head(id, 0x200, "WBFS")));
        Assert.Equal(expected, RegionDiscs.DetectRegion(Head(id, 0x58, "RVZ\x01")));
    }

    [Fact]
    public void DetectRegion_RejectsOtherGames_AndShortFiles()
    {
        Assert.Null(RegionDiscs.DetectRegion(Head("GALE01", 0)));
        Assert.Null(RegionDiscs.DetectRegion(new byte[3]));
    }

    [Fact]
    public void StoredDiscs_RoundTrip_PerRegion()
    {
        var json = RegionDiscs.WithPath("", MarioKartWiiEnums.Regions.America, "C:/roms/us.iso");
        json = RegionDiscs.WithPath(json, MarioKartWiiEnums.Regions.Europe, "C:/roms/eu.iso");

        Assert.Equal("C:/roms/us.iso", RegionDiscs.PathFor(json, MarioKartWiiEnums.Regions.America));
        Assert.Equal("C:/roms/eu.iso", RegionDiscs.PathFor(json, MarioKartWiiEnums.Regions.Europe));
        Assert.Null(RegionDiscs.PathFor(json, MarioKartWiiEnums.Regions.Japan));
        Assert.Null(
            RegionDiscs.PathFor(RegionDiscs.WithPath(json, MarioKartWiiEnums.Regions.Europe, null), MarioKartWiiEnums.Regions.Europe)
        );
        Assert.Null(RegionDiscs.PathFor("not json", MarioKartWiiEnums.Regions.America));
    }
}
