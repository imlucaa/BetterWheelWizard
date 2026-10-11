using Avalonia.Controls;
using Avalonia.Interactivity;
using WheelWizard.Features.Backups;
using WheelWizard.Services;

namespace WheelWizard.Views.Pages.Settings;

public partial class BackupSettings : UserControlBase
{
    private sealed record RegionChoice(RksysBackupSource Source)
    {
        public override string ToString() => $"{Source.Region} ({Source.GameId})";
    }

    private IReadOnlyList<RksysBackupSource> _rksysSources = [];
    private bool _ratingExists;

    public BackupSettings()
    {
        InitializeComponent();
        RefreshFileStatus();
    }

    private void RefreshFileStatus()
    {
        var previousGameId = (RksysRegionDropdown.SelectedItem as RegionChoice)?.Source.GameId;
        _rksysSources = GameDataBackupService.FindRksysFiles(PathManager.SaveFolderPath);
        RksysRegionDropdown.Items.Clear();
        foreach (var source in _rksysSources)
            RksysRegionDropdown.Items.Add(new RegionChoice(source));
        RksysRegionDropdown.SelectedItem =
            RksysRegionDropdown.Items.OfType<RegionChoice>().FirstOrDefault(choice => choice.Source.GameId == previousGameId)
            ?? RksysRegionDropdown.Items.FirstOrDefault();
        _ratingExists = GameDataBackupService.RatingFileExists(PathManager.RRratingFilePath);
        RksysStatusText.Text =
            _rksysSources.Count == 0 ? "rksys.dat was not found." : $"Detected {_rksysSources.Count} region(s). Choose one to back up.";
        RatingStatusText.Text = _ratingExists ? "RRRating.pul is ready to back up." : "RRRating.pul was not found.";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        RksysRegionDropdown.IsEnabled = _rksysSources.Count > 0;
        BackupRksysButton.IsEnabled = RksysRegionDropdown.SelectedItem is RegionChoice;
        BackupRatingButton.IsEnabled = _ratingExists;
    }

    private async void BackupRksys_OnClick(object? sender, RoutedEventArgs e)
    {
        if (RksysRegionDropdown.SelectedItem is not RegionChoice choice)
            return;
        var destination = await ChooseDestinationAsync("Choose where to back up rksys.dat");
        if (destination == null)
            return;
        RunBackup(() => GameDataBackupService.BackupRksysFile(destination, choice.Source), $"{choice.Source.Region} rksys.dat");
    }

    private void RksysRegionDropdown_OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();

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
