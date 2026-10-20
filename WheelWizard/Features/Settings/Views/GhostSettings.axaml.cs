using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WheelWizard.Ghosts;
using WheelWizard.Shared.Desktop.Storage;

namespace WheelWizard.Settings.Views;

public partial class GhostSettings : UserControl
{
    private readonly GhostImporter _importer;
    private readonly IFilePickerService _filePicker;

    // What the track list holds right now: every track (a 200cc entry only where that ghost exists), or just the ones
    // a chosen ghost could be for.
    // The search box filters it further.
    private IReadOnlyList<GhostMatch> _entries = [];
    private List<GhostMatch> _visible = [];
    private GhostMatch? _selected;
    private bool _narrowed;

    private string? _filePath;
    private TimeSpan? _fileTime;
    private bool _syncing;

    public GhostSettings(GhostImporter importer, IFilePickerService filePicker)
    {
        _importer = importer;
        _filePicker = filePicker;
        InitializeComponent();

        var available = _importer.IsAvailable;
        UnavailableText.IsVisible = !available;
        ImporterPanel.IsVisible = available;
        OpenFolderButton.IsVisible = available;
        if (available)
            ShowAllTracks();
    }

    private GhostMatch? Selected => _selected;

    private static string Label(GhostMatch entry) => entry.Speed == 200 ? $"{entry.Track.Name} (200cc)" : entry.Track.Name;

    private static bool SameEntry(GhostMatch a, GhostMatch b) =>
        a.Track.Kind == b.Track.Kind && a.Track.Index == b.Track.Index && a.Speed == b.Speed;

    private void SetEntries(IReadOnlyList<GhostMatch> entries, GhostMatch? select)
    {
        _entries = entries;
        _selected = select;
        ApplyFilter();
        ShowAllButton.IsVisible = _narrowed;
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        _visible = _entries.Where(entry => query.Length == 0 || Label(entry).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _syncing = true;
        TrackList.ItemsSource = _visible.Select(Label).ToList();
        var index = _selected is null ? -1 : _visible.FindIndex(entry => SameEntry(entry, _selected));
        TrackList.SelectedIndex = index;
        if (index >= 0)
            TrackList.ScrollIntoView(index);
        _syncing = false;
        UpdateSummary();
    }

    private void ShowAllTracks(GhostMatch? select = null)
    {
        _narrowed = false;
        var all = new List<GhostMatch>();
        foreach (var kind in new[] { GhostKind.Retro, GhostKind.Custom })
        {
            foreach (var track in _importer.LoadTracks(kind))
            {
                foreach (var speed in _importer.Speeds(track))
                    all.Add(new GhostMatch(track, speed, _importer.CurrentTime(track, speed) ?? TimeSpan.Zero));
            }
        }

        SetEntries(all, select);
        if (all.Count == 0)
            StatusText.Text = "No track list found.";
    }

    private void UpdateSummary()
    {
        var entry = Selected;
        var current = entry is null ? null : _importer.CurrentTime(entry.Track, entry.Speed);
        TrackName.Text = entry is null ? "No track picked" : Label(entry);
        CurrentText.Text =
            entry is null ? string.Empty
            : current is { } time ? $"Current ghost {GhostFile.Format(time)}"
            : "No ghost yet";

        TrackCard.IsVisible = entry is not null;
        ExportButton.IsEnabled = current is not null;

        ImportRow.IsVisible = entry is not null && _filePath is not null;
        if (entry is not null && _fileTime is { } incoming)
        {
            FileText.Text = $"Track: {Label(entry)}";
            SummaryText.Text = current is { } now
                ? $"{GhostFile.Format(now)} → {GhostFile.Format(incoming)}"
                : $"New ghost {GhostFile.Format(incoming)}";
        }
    }

    private void CancelImport_OnClick(object? sender, RoutedEventArgs e)
    {
        _filePath = null;
        _fileTime = null;
        StatusText.Text = string.Empty;
        UpdateSummary();
    }

    private void Track_OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        var index = TrackList.SelectedIndex;
        if (index >= 0 && index < _visible.Count)
            _selected = _visible[index];
        UpdateSummary();
    }

    private void Search_OnChanged(object? sender, TextChangedEventArgs e)
    {
        if (_entries.Count > 0)
            ApplyFilter();
    }

    private void ShowAll_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowAllTracks(Selected);
        StatusText.Text = string.Empty;
    }

    private async void ChooseFile_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _filePicker.OpenSingleFileAsync(
            "Choose a ghost file",
            [new FilePickerFileType("Mario Kart Wii ghost") { Patterns = ["*.rkg"], MimeTypes = ["application/octet-stream"] }]
        );
        if (path is null)
            return;

        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Reject("Could not read that file.");
            return;
        }

        if (!GhostFile.TryRead(data, out var time))
        {
            Reject("That is not a ghost file.");
            return;
        }

        _filePath = path;
        _fileTime = time;
        FileText.Text = Path.GetFileName(path);
        StatusText.Text = string.Empty;

        // A file named after a track (or with its code in the name) is taken at its word.
        var named =
            GhostImporter.GuessTrack(path, _importer.LoadTracks(GhostKind.Retro))
            ?? GhostImporter.GuessTrack(path, _importer.LoadTracks(GhostKind.Custom));
        if (named is not null)
        {
            ShowAllTracks(new GhostMatch(named, 150, TimeSpan.Zero));
            return;
        }

        // Otherwise the ghost's own header narrows it down, and the closest time goes first.
        var matches = _importer.FindMatches(data);
        if (matches.Count == 0)
        {
            ShowAllTracks();
            StatusText.Text = "Could not tell the track. Pick it above.";
            return;
        }

        _narrowed = true;
        SetEntries(matches, matches[0]);
        StatusText.Text = matches.Count == 1 ? string.Empty : "Best match picked. Check it above.";
    }

    private void Reject(string message)
    {
        _filePath = null;
        _fileTime = null;
        StatusText.Text = message;
        UpdateSummary();
    }

    private void Import_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry || _filePath is null)
            return;

        var result = _importer.Import(_filePath, entry.Track, entry.Speed);
        StatusText.Text = result.Success ? "Done." : result.Message;
        if (!result.Success)
            return;

        _filePath = null;
        _fileTime = null;
        UpdateSummary();
    }

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } entry)
            return;

        var path = await _filePicker.SaveFileAsync(
            "Save the ghost",
            [new FilePickerFileType("Mario Kart Wii ghost") { Patterns = ["*.rkg"], MimeTypes = ["application/octet-stream"] }],
            _importer.ExportFileName(entry.Track, entry.Speed)
        );
        if (path is null)
            return;

        StatusText.Text = _importer.Export(entry.Track, entry.Speed, path).Message;
    }

    private void OpenFolder_OnClick(object? sender, RoutedEventArgs e) => _filePicker.OpenFolderInFileManager(_importer.GhostsFolder);
}
