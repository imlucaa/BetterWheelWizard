using Avalonia.Interactivity;
using WheelWizard.Features.Backups;
using WheelWizard.Services;

namespace WheelWizard.Views.Pages.Settings;

public partial class BackupSettings : UserControlBase
{
    private int _rksysCount;
    private bool _ratingExists;

    public BackupSettings()
    {
        InitializeComponent();
        RefreshFileStatus();
    }

    private void RefreshFileStatus()
    {
        _rksysCount = GameDataBackupService.FindRksysFiles(PathManager.SaveFolderPath).Count;
        _ratingExists = GameDataBackupService.RatingFileExists(PathManager.RRratingFilePath);
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
        RunBackup(() => GameDataBackupService.BackupRksysFiles(destination, PathManager.SaveFolderPath), "rksys.dat");
    }

    private async void BackupRating_OnClick(object? sender, RoutedEventArgs e)
    {
        var destination = await ChooseDestinationAsync("Choose where to back up RRRating.pul");
        if (destination == null)
            return;
        RunBackup(() => GameDataBackupService.BackupRatingFile(destination, PathManager.RRratingFilePath), "RRRating.pul");
    }

    private static async Task<string?> ChooseDestinationAsync(string title)
    {
        var folders = await FilePickerHelper.SelectFolderAsync(title);
        return FilePickerHelper.TryResolveLocalPath(folders.FirstOrDefault());
    }

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
