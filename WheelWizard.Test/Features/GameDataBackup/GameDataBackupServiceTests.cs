using Testably.Abstractions.Testing;
using WheelWizard.Features.Backups;
using WheelWizard.Models.Enums;

namespace WheelWizard.Test.Features.GameDataBackup;

public class GameDataBackupServiceTests
{
    private static MockFileSystem TwoRegionSaves()
    {
        var fs = new MockFileSystem();
        fs.Directory.CreateDirectory("/saves/RMCE");
        fs.File.WriteAllText("/saves/RMCE/rksys.dat", "us");
        fs.Directory.CreateDirectory("/saves/RMCP");
        fs.File.WriteAllText("/saves/RMCP/rksys.dat", "eu");
        fs.File.SetLastWriteTimeUtc("/saves/RMCE/rksys.dat", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        fs.File.SetLastWriteTimeUtc("/saves/RMCP/rksys.dat", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return fs;
    }

    [Fact]
    public void FindRksysSaves_ReportsRegionAndLastWrite()
    {
        var saves = new GameDataBackupService(TwoRegionSaves()).FindRksysSaves("/saves");

        Assert.Equal(2, saves.Count);
        Assert.Equal(MarioKartWiiEnums.Regions.Europe, saves.OrderByDescending(save => save.LastWriteUtc).First().Region);
    }

    [Fact]
    public void BackupRksysFiles_WithRegion_CopiesOnlyThatRegion()
    {
        var fs = TwoRegionSaves();

        var result = new GameDataBackupService(fs).BackupRksysFiles("/out", "/saves", [MarioKartWiiEnums.Regions.America]);

        Assert.Single(result.CopiedFiles);
        Assert.True(fs.File.Exists("/out/RMCE/rksys.dat"));
        Assert.False(fs.File.Exists("/out/RMCP/rksys.dat"));
    }

    [Fact]
    public void BackupRksysFiles_WithoutRegion_CopiesEverything()
    {
        var result = new GameDataBackupService(TwoRegionSaves()).BackupRksysFiles("/out", "/saves");

        Assert.Equal(2, result.CopiedFiles.Count);
    }
}
