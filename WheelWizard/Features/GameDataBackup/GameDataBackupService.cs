using System.IO.Abstractions;
using WheelWizard.Models.Enums;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.Features.Backups;

public sealed record RksysSave(MarioKartWiiEnums.Regions Region, string Path, DateTime LastWriteUtc);

public sealed record FileBackupResult(string DestinationFolder, IReadOnlyList<string> CopiedFiles);

public sealed class GameDataBackupService(IFileSystem fileSystem)
{
    public IReadOnlyList<RksysSave> FindRksysSaves(string saveFolderPath)
    {
        if (string.IsNullOrWhiteSpace(saveFolderPath))
            return [];

        return Enum.GetValues<MarioKartWiiEnums.Regions>()
            .Where(region => region != MarioKartWiiEnums.Regions.None)
            .Select(region => new
            {
                Region = region,
                Path = fileSystem.Path.Combine(saveFolderPath, GameRegion.GetGameId(region), "rksys.dat"),
            })
            .Where(save => fileSystem.File.Exists(save.Path))
            .Select(save => new RksysSave(save.Region, save.Path, fileSystem.File.GetLastWriteTimeUtc(save.Path)))
            .ToList();
    }

    public IReadOnlyList<string> FindRksysFiles(string saveFolderPath) => FindRksysSaves(saveFolderPath).Select(save => save.Path).ToList();

    public bool RatingFileExists(string ratingFilePath) =>
        !string.IsNullOrWhiteSpace(ratingFilePath) && fileSystem.File.Exists(ratingFilePath);

    public FileBackupResult BackupRksysFiles(
        string destinationFolder,
        string saveFolderPath,
        IReadOnlyCollection<MarioKartWiiEnums.Regions>? regions = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        var sources = FindRksysSaves(saveFolderPath)
            .Where(save => regions == null || regions.Contains(save.Region))
            .Select(save => save.Path)
            .ToList();
        if (sources.Count == 0)
            throw new FileNotFoundException("No rksys.dat files were found to back up.");

        fileSystem.Directory.CreateDirectory(destinationFolder);
        var copiedFiles = new List<string>();
        foreach (var source in sources)
        {
            var gameId = fileSystem.Path.GetFileName(fileSystem.Path.GetDirectoryName(source)) ?? "MKWii";
            var regionFolder = fileSystem.Path.Combine(destinationFolder, gameId);
            fileSystem.Directory.CreateDirectory(regionFolder);
            var destination = fileSystem.Path.Combine(regionFolder, "rksys.dat");
            fileSystem.File.Copy(source, destination, overwrite: true);
            copiedFiles.Add(destination);
        }

        return new(destinationFolder, copiedFiles);
    }

    public FileBackupResult BackupRatingFile(string destinationFolder, string ratingFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        if (!RatingFileExists(ratingFilePath))
            throw new FileNotFoundException("RRRating.pul was not found.");

        fileSystem.Directory.CreateDirectory(destinationFolder);
        var destination = fileSystem.Path.Combine(destinationFolder, "RRRating.pul");
        fileSystem.File.Copy(ratingFilePath, destination, overwrite: true);
        return new(destinationFolder, [destination]);
    }
}
