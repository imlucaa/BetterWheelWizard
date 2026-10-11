using WheelWizard.Models.Enums;
using WheelWizard.Services.Other;

namespace WheelWizard.Features.Backups;

public sealed record FileBackupResult(string DestinationFolder, IReadOnlyList<string> CopiedFiles);

public sealed record RksysBackupSource(MarioKartWiiEnums.Regions Region, string GameId, string FilePath);

public static class GameDataBackupService
{
    public static IReadOnlyList<RksysBackupSource> FindRksysFiles(string saveFolderPath)
    {
        if (string.IsNullOrWhiteSpace(saveFolderPath))
            return [];

        return Enum.GetValues<MarioKartWiiEnums.Regions>()
            .Where(region => region != MarioKartWiiEnums.Regions.None)
            .Select(region =>
            {
                var gameId = RRRegionManager.ConvertRegionToGameId(region);
                return new RksysBackupSource(region, gameId, Path.Combine(saveFolderPath, gameId, "rksys.dat"));
            })
            .Where(source => File.Exists(source.FilePath))
            .ToList();
    }

    public static bool RatingFileExists(string ratingFilePath) => !string.IsNullOrWhiteSpace(ratingFilePath) && File.Exists(ratingFilePath);

    public static FileBackupResult BackupRksysFiles(string destinationFolder, string saveFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        var sources = FindRksysFiles(saveFolderPath);
        if (sources.Count == 0)
            throw new FileNotFoundException("No rksys.dat files were found to back up.");

        Directory.CreateDirectory(destinationFolder);
        var copiedFiles = new List<string>();
        foreach (var source in sources)
        {
            var regionFolder = Path.Combine(destinationFolder, GetRegionFolderName(source));
            Directory.CreateDirectory(regionFolder);
            var destination = Path.Combine(regionFolder, "rksys.dat");
            File.Copy(source.FilePath, destination, overwrite: true);
            copiedFiles.Add(destination);
        }

        return new(destinationFolder, copiedFiles);
    }

    public static FileBackupResult BackupRksysFile(string destinationFolder, RksysBackupSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        if (!File.Exists(source.FilePath))
            throw new FileNotFoundException("The selected rksys.dat file was not found.", source.FilePath);
        var regionFolder = Path.Combine(destinationFolder, GetRegionFolderName(source));
        Directory.CreateDirectory(regionFolder);
        var destination = Path.Combine(regionFolder, "rksys.dat");
        File.Copy(source.FilePath, destination, overwrite: true);
        return new(destinationFolder, [destination]);
    }

    public static string GetRegionFolderName(RksysBackupSource source) => $"{source.Region} ({source.GameId})";

    public static FileBackupResult BackupRatingFile(string destinationFolder, string ratingFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        if (!RatingFileExists(ratingFilePath))
            throw new FileNotFoundException("RRRating.pul was not found.");

        Directory.CreateDirectory(destinationFolder);
        var destination = Path.Combine(destinationFolder, "RRRating.pul");
        File.Copy(ratingFilePath, destination, overwrite: true);
        return new(destinationFolder, [destination]);
    }
}
