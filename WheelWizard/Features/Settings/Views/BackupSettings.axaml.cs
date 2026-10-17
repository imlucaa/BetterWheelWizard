using System.IO.Abstractions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WheelWizard.CustomDistributions;
using WheelWizard.Dolphin.Paths;
using WheelWizard.Features.Backups;
using WheelWizard.Models.Enums;
using WheelWizard.Shared.Desktop.Storage;
using WheelWizard.Views.Dialogs;
using WheelWizard.WiiManagement;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.Settings.Views;

public partial class BackupSettings : UserControl
{
    private int _rksysCount;
    private bool _ratingExists;
    private readonly GameDataBackupService _backups;
    private readonly ICustomDistributionPaths _distributionPaths;
    private readonly IDolphinPaths _dolphinPaths;
    private readonly IFilePickerService _filePicker;
    private readonly IFileSystem _fileSystem;
    private readonly IPopupFactory _popups;

    public BackupSettings(
        GameDataBackupService backups,
        ICustomDistributionPaths distributionPaths,
        IDolphinPaths dolphinPaths,
        IFilePickerService filePicker,
        IFileSystem fileSystem,
        IPopupFactory popups
    )
    {
        _backups = backups;
        _distributionPaths = distributionPaths;
        _dolphinPaths = dolphinPaths;
        _filePicker = filePicker;
        _fileSystem = fileSystem;
        _popups = popups;
        InitializeComponent();
        RefreshFileStatus();
    }

    private void RefreshFileStatus()
    {
        _rksysCount = _backups.FindRksysFiles(_distributionPaths.SaveFolderPath).Count;
        _ratingExists = _backups.RatingFileExists(RatingFilePath);
        RksysStatusText.Text = _rksysCount == 0 ? "rksys.dat was not found." : $"Found {_rksysCount} regional rksys.dat file(s).";
        RatingStatusText.Text = _ratingExists ? "RRRating.pul is ready to back up." : "RRRating.pul was not found.";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        BackupRksysButton.IsEnabled = _rksysCount > 0;
        BackupRatingButton.IsEnabled = _ratingExists;
    }

    private async void BackupRksys_OnClick(object? sender, RoutedEventArgs e)
    {
        var saves = _backups.FindRksysSaves(_distributionPaths.SaveFolderPath);
        IReadOnlyCollection<MarioKartWiiEnums.Regions>? regions = null;
        if (saves.Count > 1)
        {
            regions = await ChooseRegionsAsync(saves);
            if (regions == null)
                return;
        }

        var destination = await ChooseDestinationAsync("Choose where to back up rksys.dat");
        if (destination == null)
            return;
        RunBackup(() => _backups.BackupRksysFiles(destination, _distributionPaths.SaveFolderPath, regions), "rksys.dat");
    }

    /// <summary>Asks which region to back up; the most recently played save is offered first as the current one.</summary>
    private async Task<IReadOnlyCollection<MarioKartWiiEnums.Regions>?> ChooseRegionsAsync(IReadOnlyList<RksysSave> saves)
    {
        var current = saves.OrderByDescending(save => save.LastWriteUtc).First();
        var options = new List<ChoiceOption> { new($"Current region — {RegionName(current.Region)}", "Detected", true) };
        var results = new List<IReadOnlyCollection<MarioKartWiiEnums.Regions>> { new[] { current.Region } };

        options.Add(new("All regions", $"{saves.Count} saves"));
        results.Add(saves.Select(save => save.Region).ToArray());

        foreach (var save in saves)
        {
            options.Add(new(RegionName(save.Region), GameRegion.GetGameId(save.Region)));
            results.Add(new[] { save.Region });
        }

        var index = await _popups
            .CreateChoiceList(
                "Back up rksys.dat",
                "Which region?",
                "Pick the save to back up, or let it use the region you played last.",
                options
            )
            .AwaitAnswer();
        return index is >= 0 && index < results.Count ? results[index.Value] : null;
    }

    private static string RegionName(MarioKartWiiEnums.Regions region) =>
        region switch
        {
            MarioKartWiiEnums.Regions.America => "Americas",
            MarioKartWiiEnums.Regions.Europe => "Europe",
            MarioKartWiiEnums.Regions.Japan => "Japan",
            MarioKartWiiEnums.Regions.Korea => "Korea",
            _ => region.ToString(),
        };

    private async void BackupRating_OnClick(object? sender, RoutedEventArgs e)
    {
        var destination = await ChooseDestinationAsync("Choose where to back up RRRating.pul");
        if (destination == null)
            return;
        RunBackup(() => _backups.BackupRatingFile(destination, RatingFilePath), "RRRating.pul");
    }

    private async Task<string?> ChooseDestinationAsync(string title)
    {
        var folders = await _filePicker.SelectFolderAsync(title);
        return StoragePaths.TryResolveLocalPath(folders.FirstOrDefault());
    }

    private string RatingFilePath => _fileSystem.Path.GetRrRatingFilePath(_dolphinPaths.WiiFolderPath);

    private void RunBackup(Func<FileBackupResult> backup, string label)
    {
        BackupRksysButton.IsEnabled = false;
        BackupRatingButton.IsEnabled = false;
        BackupResultText.IsVisible = true;
        BackupResultText.Text = $"Backing up {label}…";
        try
        {
            var result = backup();
            BackupResultText.Text = $"Backed up {result.CopiedFiles.Count} file(s) to {result.DestinationFolder}.";
        }
        catch (Exception exception)
        {
            BackupResultText.Text = exception.Message;
        }
        finally
        {
            RefreshFileStatus();
        }
    }
}
