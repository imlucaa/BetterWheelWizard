using System.IO.Abstractions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WheelWizard.CustomDistributions;
using WheelWizard.Dolphin.Paths;
using WheelWizard.Features.Backups;
using WheelWizard.Views.Storage;
using WheelWizard.WiiManagement;

namespace WheelWizard.Views.Pages.Settings;

public partial class BackupSettings : UserControl
{
    private int _rksysCount;
    private bool _ratingExists;
    private readonly GameDataBackupService _backups;
    private readonly ICustomDistributionPaths _distributionPaths;
    private readonly IDolphinPaths _dolphinPaths;
    private readonly IFilePickerService _filePicker;
    private readonly IFileSystem _fileSystem;

    public BackupSettings(
        GameDataBackupService backups,
        ICustomDistributionPaths distributionPaths,
        IDolphinPaths dolphinPaths,
        IFilePickerService filePicker,
        IFileSystem fileSystem
    )
    {
        _backups = backups;
        _distributionPaths = distributionPaths;
        _dolphinPaths = dolphinPaths;
        _filePicker = filePicker;
        _fileSystem = fileSystem;
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
        var destination = await ChooseDestinationAsync("Choose where to back up rksys.dat");
        if (destination == null)
            return;
        RunBackup(() => _backups.BackupRksysFiles(destination, _distributionPaths.SaveFolderPath), "rksys.dat");
    }

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
