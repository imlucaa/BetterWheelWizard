using WheelWizard.Models.Enums;
using WheelWizard.Services.Other;

namespace WheelWizard.Features.Backups;

public sealed record FileBackupResult(string DestinationFolder, IReadOnlyList<string> CopiedFiles);

public static class GameDataBackupService
{
    public static IReadOnlyList<string> FindRksysFiles(string saveFolderPath)
    {
        if (string.IsNullOrWhiteSpace(saveFolderPath))
            return [];

        return Enum.GetValues<MarioKartWiiEnums.Regions>()
            .Where(region => region != MarioKartWiiEnums.Regions.None)
            .Select(region => Path.Combine(saveFolderPath, RRRegionManager.ConvertRegionToGameId(region), "rksys.dat"))
            .Where(File.Exists)
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
            var gameId = Path.GetFileName(Path.GetDirectoryName(source)) ?? "MKWii";
            var regionFolder = Path.Combine(destinationFolder, gameId);
            Directory.CreateDirectory(regionFolder);
            var destination = Path.Combine(regionFolder, "rksys.dat");
            File.Copy(source, destination, overwrite: true);
            copiedFiles.Add(destination);
        }

        return new(destinationFolder, copiedFiles);
    }

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
