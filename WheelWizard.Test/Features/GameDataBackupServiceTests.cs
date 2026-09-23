using WheelWizard.Features.Backups;

namespace WheelWizard.Test.Features;

public class GameDataBackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"BetterWheelWizard-backup-tests-{Guid.NewGuid():N}");

    [Fact]
    public void BackupRksysFiles_CopiesEachDetectedRegionWithoutZip()
    {
        var saveFolder = Path.Combine(_root, "Save");
        WriteFile(Path.Combine(saveFolder, "RMCE", "rksys.dat"), [1, 2]);
        WriteFile(Path.Combine(saveFolder, "RMCP", "rksys.dat"), [3, 4]);
        var destination = Path.Combine(_root, "Backup");

        var result = GameDataBackupService.BackupRksysFiles(destination, saveFolder);

        Assert.Equal(2, result.CopiedFiles.Count);
        Assert.Equal([1, 2], File.ReadAllBytes(Path.Combine(destination, "RMCE", "rksys.dat")));
        Assert.Equal([3, 4], File.ReadAllBytes(Path.Combine(destination, "RMCP", "rksys.dat")));
        Assert.Empty(Directory.GetFiles(destination, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public void BackupRatingFile_CopiesPulFileDirectly()
    {
        var ratingPath = Path.Combine(_root, "Source", "RRRating.pul");
        WriteFile(ratingPath, [5, 6, 7]);
        var destination = Path.Combine(_root, "Backup");

        var result = GameDataBackupService.BackupRatingFile(destination, ratingPath);

        Assert.Single(result.CopiedFiles);
        Assert.Equal([5, 6, 7], File.ReadAllBytes(Path.Combine(destination, "RRRating.pul")));
    }

    [Fact]
    public void BackupRksysFiles_ThrowsWhenNoSaveExists()
    {
        Assert.Throws<FileNotFoundException>(
            () => GameDataBackupService.BackupRksysFiles(Path.Combine(_root, "Backup"), Path.Combine(_root, "Save"))
        );
    }

    [Fact]
    public void BackupRatingFile_ThrowsWhenRatingDoesNotExist()
    {
        Assert.Throws<FileNotFoundException>(
            () => GameDataBackupService.BackupRatingFile(Path.Combine(_root, "Backup"), Path.Combine(_root, "missing.pul"))
        );
    }

    private static void WriteFile(string path, byte[] contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
