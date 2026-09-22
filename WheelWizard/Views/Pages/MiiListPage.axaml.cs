using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Testably.Abstractions;
using WheelWizard.CustomCharacters;
using WheelWizard.Helpers;
using WheelWizard.RrRooms;
using WheelWizard.Services;
using WheelWizard.Settings;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Shared.MessageTranslations;
using WheelWizard.Shared.Services;
using WheelWizard.Views.Components;
using WheelWizard.Views.Patterns;
using WheelWizard.Views.Popups.Generic;
using WheelWizard.Views.Popups.MiiManagement;
using WheelWizard.WiiManagement;
using WheelWizard.WiiManagement.MiiManagement;
using WheelWizard.WiiManagement.MiiManagement.Domain.Mii;

namespace WheelWizard.Views.Pages;

public sealed record SuggestedOnlineMii(Mii Mii, string Name, string FriendCode);

public partial class MiiListPage : UserControlBase
{
    public ObservableCollection<MiiListRow> MiiRows { get; } = [];
    public ObservableCollection<SuggestedOnlineMii> SuggestedOnlineMiis { get; } = [];
    private readonly List<MiiListEntry> _miiEntries = [];
    private Mii? _selectedOnlineMii;
    private bool _isPageReady;

    [Inject]
    private ICustomCharactersService CustomCharactersService { get; set; } = null!;

    [Inject]
    private IMiiDbService MiiDbService { get; set; } = null!;

    [Inject]
    private IMiiRepositoryService MiiRepositoryService { get; set; } = null!;

    [Inject]
    private IFileSystem FileSystem { get; set; } = null!;

    [Inject]
    private IRandomSystem Random { get; set; } = null!;

    [Inject]
    private ISettingsManager SettingsService { get; set; } = null!;

    [Inject]
    private IApiCaller<IRwfcApi> ApiCaller { get; set; } = null!;

    [Inject]
    private IRrLeaderboardSingletonService LeaderboardService { get; set; } = null!;

    private bool _suggestionsLoaded;

    public MiiListPage()
    {
        InitializeComponent();
        DataContext = this;
        _isPageReady = true;

        var miiDbExists = MiiDbService.Exists();
        if (!miiDbExists)
        {
            if (SettingsService.PathsSetupCorrectly())
            {
                var creationResult = MiiRepositoryService.ForceCreateDatabase();
                if (creationResult.IsFailure)
                {
                    MessageTranslationHelper.ShowMessage(creationResult.Error);
                    VisibleWhenNoDb.IsVisible = !miiDbExists;
                }
            }
            else
            {
                VisibleWhenDb.IsVisible = false;
                VisibleWhenNoDb.IsVisible = true;
            }
        }

        miiDbExists = MiiDbService.Exists();
        if (!miiDbExists)
            return;

        VisibleWhenDb.IsVisible = true;
        VisibleWhenNoDb.IsVisible = false;
        ReloadMiiList();
    }

    private void MyMiisTab_OnClick(object? sender, RoutedEventArgs e) => ShowOnlineMiis(false);

    private void OnlineMiisTab_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowOnlineMiis(true);
        if (!_suggestionsLoaded)
            _ = LoadSuggestedMiisAsync();
    }

    private void ShowOnlineMiis(bool showOnline)
    {
        MiiList.IsVisible = !showOnline;
        SavedMiiSearchField.IsVisible = !showOnline;
        OnlineMiiPanel.IsVisible = showOnline;
    }

    private void SavedMiiSearch_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isPageReady)
            return;

        ClearSelectedEntries();
        RebuildRows(SavedMiiSearchField.Text);
        ChangeTopButtons();
    }

    private void ToggleSavedMiiSearch_OnClick(object? sender, RoutedEventArgs e)
    {
        SavedMiiSearchField.IsVisible = !SavedMiiSearchField.IsVisible;
        if (SavedMiiSearchField.IsVisible)
        {
            SavedMiiSearchField.Focus();
            return;
        }

        SavedMiiSearchField.Text = string.Empty;
    }

    private void OnlineMiiSearch_OnTextChanged(object? sender, TextChangedEventArgs e) => OnlineMiiLookupStatus.Text = string.Empty;

    private void OnlineMiiSearch_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        SearchFriendCode();
    }

    private void SearchFriendCode_OnClick(object? sender, RoutedEventArgs e) => SearchFriendCode();

    private async void SearchFriendCode()
    {
        var normalizedFriendCode = NormalizeFriendCode(OnlineMiiSearchField.Text);
        if (normalizedFriendCode == null)
        {
            SetOnlineLookupStatus("Enter all 12 digits of the friend code.", isError: true);
            return;
        }

        SearchOnlineMiiButton.IsEnabled = false;
        OnlineMiiSearchField.IsEnabled = false;
        SetOnlineLookupStatus($"Looking up {normalizedFriendCode}…", isError: false);

        try
        {
            var result = await ApiCaller.CallApiAsync(api => api.GetPlayerProfileAsync(normalizedFriendCode));
            if (result.IsFailure || result.Value == null)
            {
                SetOnlineLookupStatus($"No RWFC profile was found for {normalizedFriendCode}.", isError: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(result.Value.MiiData))
            {
                SetOnlineLookupStatus("That RWFC profile does not have Mii data.", isError: true);
                return;
            }

            var miiResult = MiiSerializer.Deserialize(result.Value.MiiData);
            if (miiResult.IsFailure)
            {
                SetOnlineLookupStatus("RWFC returned Mii data that Wheel Wizard could not read.", isError: true);
                return;
            }

            SetOnlineMii(miiResult.Value, result.Value.Name, normalizedFriendCode);
            SetOnlineLookupStatus($"Loaded from RWFC · {result.Value.Vr:N0} VR", isError: false);
        }
        finally
        {
            SearchOnlineMiiButton.IsEnabled = true;
            OnlineMiiSearchField.IsEnabled = true;
            OnlineMiiSearchField.Focus();
        }
    }

    private static string? NormalizeFriendCode(string? friendCode)
    {
        var digits = new string((friendCode ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 12 ? $"{digits[..4]}-{digits.Substring(4, 4)}-{digits.Substring(8, 4)}" : null;
    }

    private void SetOnlineMii(Mii mii, string playerName, string friendCode)
    {
        _selectedOnlineMii = mii;
        OnlineMiiPreview.Mii = mii;
        OnlineMiiName.Text = string.IsNullOrWhiteSpace(playerName) ? mii.Name.ToString() : playerName;
        OnlineMiiFriendCode.Text = friendCode;
        ImportOnlineMiiButton.IsEnabled = true;
    }

    private void SetOnlineLookupStatus(string message, bool isError)
    {
        OnlineMiiLookupStatus.Text = message;
        OnlineMiiLookupStatus.Foreground = new Avalonia.Media.SolidColorBrush(
            isError ? ViewUtils.Colors.Danger400 : ViewUtils.Colors.Neutral400
        );
    }

    private async void RefreshSuggestedMiis_OnClick(object? sender, RoutedEventArgs e) => await LoadSuggestedMiisAsync();

    private async Task LoadSuggestedMiisAsync()
    {
        RefreshSuggestedMiisButton.IsEnabled = false;
        SuggestedMiisStatus.Text = "Finding random public Miis…";
        try
        {
            var result = await LeaderboardService.GetTopPlayersAsync(200);
            if (result.IsFailure)
            {
                SuggestedMiisStatus.Text = "Random Miis could not be loaded from RWFC. Try again.";
                return;
            }

            var suggestions = result
                .Value.Where(entry => !string.IsNullOrWhiteSpace(entry.MiiData) && !string.IsNullOrWhiteSpace(entry.FriendCode))
                .OrderBy(_ => Guid.NewGuid())
                .Take(8)
                .Select(entry =>
                {
                    var parsed = MiiSerializer.Deserialize(entry.MiiData!);
                    return parsed.IsSuccess
                        ? new SuggestedOnlineMii(
                            parsed.Value,
                            string.IsNullOrWhiteSpace(entry.Name) ? parsed.Value.Name.ToString() : entry.Name,
                            entry.FriendCode
                        )
                        : null;
                })
                .Where(item => item != null)
                .Cast<SuggestedOnlineMii>()
                .ToList();

            SuggestedOnlineMiis.Clear();
            foreach (var suggestion in suggestions)
                SuggestedOnlineMiis.Add(suggestion);

            _suggestionsLoaded = suggestions.Count > 0;
            SuggestedMiisStatus.Text =
                suggestions.Count > 0
                    ? "Refresh for a different set. Download adds a new copy to My Miis."
                    : "RWFC did not return any downloadable Miis. Try again later.";
        }
        finally
        {
            RefreshSuggestedMiisButton.IsEnabled = true;
        }
    }

    private void DownloadSuggestedMii_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SuggestedOnlineMii suggestion })
            return;

        ImportOnlineMii(suggestion.Mii);
    }

    private void ImportOnlineMii_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedOnlineMii == null)
            return;

        ImportOnlineMii(_selectedOnlineMii);
    }

    private void ImportOnlineMii(Mii sourceMii)
    {
        var serialized = MiiSerializer.Serialize(sourceMii);
        if (serialized.IsFailure)
        {
            ViewUtils.ShowSnackbar(serialized.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        var copyResult = MiiSerializer.Deserialize(serialized.Value);
        if (copyResult.IsFailure)
        {
            ViewUtils.ShowSnackbar(copyResult.Error.Message, ViewUtils.SnackbarType.Danger);
            return;
        }

        var saveResult = MiiDbService.AddToDatabase(copyResult.Value, "02:11:11:11:11:11");
        if (saveResult.IsFailure)
        {
            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_save", saveResult.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return;
        }

        ReloadMiiList();
        ViewUtils.ShowSnackbar(t("snackbar_success.mii_added"));
    }

    #region Multi and Single select

    private bool _isShiftPressed;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Subscribe to keyboard events to track Shift key state
        KeyDown += MiiListPage_KeyDown;
        KeyUp += MiiListPage_KeyUp;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Unsubscribe from keyboard events when control is detached
        KeyDown -= MiiListPage_KeyDown;
        KeyUp -= MiiListPage_KeyUp;

        base.OnDetachedFromVisualTree(e);
    }

    private void MiiListPage_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl))
            return;

        _isShiftPressed = true;
    }

    private void MiiListPage_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl))
            return;

        _isShiftPressed = false;
    }

    #endregion

    private void ReloadMiiList()
    {
        _miiEntries.Clear();
        foreach (var mii in MiiDbService.GetAllMiis().OrderByDescending(m => m.IsFavorite))
        {
            _miiEntries.Add(new MiiListEntry(mii));
        }

        var count = _miiEntries.Count;
        ListItemCount.Text = count.ToString();

        if (count < 100)
            _miiEntries.Add(MiiListEntry.CreateAddEntry());

        RebuildRows(SavedMiiSearchField.Text);
        ChangeTopButtons();
    }

    private void RebuildRows(string? searchText = null)
    {
        var query = searchText?.Trim();
        var visibleEntries = string.IsNullOrWhiteSpace(query)
            ? _miiEntries
            : _miiEntries.Where(entry => entry.Mii?.Name.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) == true).ToList();

        MiiRows.Clear();
        for (var i = 0; i < visibleEntries.Count; i += 4)
        {
            var chunk = visibleEntries.Skip(i).Take(4).ToList();
            MiiRows.Add(new MiiListRow(chunk));
        }
    }

    private void ClearSelectedEntries()
    {
        foreach (var entry in _miiEntries)
            entry.IsSelected = false;
    }

    private void DeleteMii_OnClick(object? sender, RoutedEventArgs e) => DeleteMii(GetSelectedMiis());

    private void EditMii_OnClick(object? sender, RoutedEventArgs e) => EditMii(GetSelectedMiis()[0]);

    private void FavMii_OnClick(object? sender, RoutedEventArgs e) => ToggleFavorite(GetSelectedMiis());

    private void ExportMii_OnClick(object? sender, RoutedEventArgs e) => ExportMultipleMiiFiles(GetSelectedMiis());

    private void DuplicateMii_OnClick(object? sender, RoutedEventArgs e) => DuplicateMii(GetSelectedMiis());

    private async void ImportMii_OnClick(object? sender, RoutedEventArgs e)
    {
        var miiFiles = await FilePickerHelper.OpenFilePickerAsync(
            fileType: CustomFilePickerFileType.Miis,
            allowMultiple: true,
            title: "Select Mii file(s)"
        );
        if (miiFiles.Count == 0)
            return;
        foreach (var file in miiFiles)
        {
            FileSystem.File.Exists(file);
            //get raw bytes from file
            var stream = FileSystem.File.OpenRead(file);
            using var reader = new BinaryReader(stream);
            var miiData = reader.ReadBytes((int)stream.Length);
            stream.Close();
            var result = MiiSerializer.Deserialize(miiData);
            if (result.IsFailure)
            {
                ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_deserialize", result.Error.Message)!, ViewUtils.SnackbarType.Danger);
                return;
            }

            var mii = result.Value;

            //We duplicate to make sure it does not actually have the original MiiId
            var macAddress = "02:11:11:11:11:11";
            var saveResult = MiiDbService.AddToDatabase(mii, macAddress);
            if (saveResult.IsFailure)
            {
                ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_save", saveResult.Error.Message)!, ViewUtils.SnackbarType.Danger);
                return;
            }
        }

        ReloadMiiList();
    }

    private void MiiBlock_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MiiBlock { DataContext: MiiListEntry entry })
            return;

        if (entry.IsAddEntry)
        {
            ClearSelectedEntries();
            ChangeTopButtons();
            CreateNewMii();
            return;
        }

        if (!_isShiftPressed)
        {
            var shouldSelectThis = entry.IsSelected;
            foreach (var item in _miiEntries)
                item.IsSelected = false;

            entry.IsSelected = shouldSelectThis;
        }

        ChangeTopButtons();
    }

    private void FavoriteContextMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetMenuMii(sender, out var mii))
            ContextAction(mii, ToggleFavorite);
    }

    private void EditContextMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetMenuMii(sender, out var mii))
            EditMii(mii);
    }

    private void DuplicateContextMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetMenuMii(sender, out var mii))
            ContextAction(mii, DuplicateMii);
    }

    private void ExportContextMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetMenuMii(sender, out var mii))
            ContextAction(mii, ExportMultipleMiiFiles);
    }

    private void DeleteContextMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetMenuMii(sender, out var mii))
            ContextAction(mii, DeleteMii);
    }

    private static bool TryGetMenuMii(object? sender, out Mii mii)
    {
        if (sender is MenuItem { CommandParameter: MiiListEntry { Mii: not null } entry })
        {
            mii = entry.Mii!;
            return true;
        }

        mii = null!;
        return false;
    }

    private void ToggleFavorite(Mii[] miis)
    {
        var allFavorite = miis.All(m => m.IsFavorite);

        foreach (var mii in miis)
        {
            mii.IsFavorite = !allFavorite;
            var result = MiiDbService.Update(mii);
            if (result.IsFailure)
            {
                ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_update", result.Error.Message)!, ViewUtils.SnackbarType.Danger);
                return;
            }
        }

        ReloadMiiList();
    }

    private void ExportMultipleMiiFiles(Mii[] miis)
    {
        if (miis.Length == 0)
        {
            ViewUtils.ShowSnackbar(t("snackbar_warning.no_mii_export"), ViewUtils.SnackbarType.Warning);
            return;
        }

        foreach (var mii in miis)
        {
            ExportMiiAsFile(mii);
        }
    }

    public static string ReplaceInvalidFileNameChars(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private async void ExportMiiAsFile(Mii mii)
    {
        var exportName = ReplaceInvalidFileNameChars(CustomCharactersService.NormalizeToAscii(mii.Name.ToString()));
        var diaglog = await FilePickerHelper.SaveFileAsync(
            title: "Save Mii as file",
            fileTypes: [CustomFilePickerFileType.Miis],
            defaultFileName: $"{exportName}"
        );
        if (diaglog == null)
            return;
        var result = MiiDbService.GetByAvatarId(mii.MiiId);
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_get", result.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return;
        }

        var miiToExport = result.Value;
        var saveResult = SaveMiiToDisk(miiToExport, diaglog);
        if (saveResult.IsFailure)
        {
            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_save", saveResult.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return;
        }

        ViewUtils.ShowSnackbar(t("snackbar_success.saved_mii", miiToExport.Name, diaglog) ?? "Saved Mii successfully");
    }

    private OperationResult SaveMiiToDisk(Mii mii, string path)
    {
        var miiData = MiiSerializer.Serialize(mii);
        if (miiData.IsFailure)
        {
            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_serialize", miiData.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return miiData;
        }

        var file = FileSystem.FileInfo.New(path);
        using var stream = file.Open(FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write(miiData.Value);
        writer.Flush();
        writer.Close();
        stream.Close();
        ViewUtils.ShowSnackbar(t("snackbar_success.saved_mii", mii.Name, file.FullName) ?? "Saved Mii successfully");
        return Ok();
    }

    private async void DeleteMii(Mii[] miis)
    {
        if (miis.Length == 0)
        {
            ViewUtils.ShowSnackbar(t("snackbar_warning.no_mii_delete"), ViewUtils.SnackbarType.Warning);
            return;
        }

        // TODO: add a check that you cant remove a Mii that is in use by a licence,
        // I have no idea how tho

        if (miis.Any(mii => mii.IsFavorite))
        {
            await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Warning_CantDeleteFavMii);
            return;
        }

        var mainText = t("question.sure_delete.title_miis", miis.Length) ?? $"Delete {miis.Length}?";
        var successMessage = t("snackbar_success.deleted_miis", miis.Length) ?? $"Deleted {miis.Length}";
        if (miis.Length == 1)
        {
            mainText = t("question.sure_delete.title", miis[0].Name) ?? $"Delete {miis[0].Name}?";
            successMessage = t("snackbar_success.deleted", miis[0].Name) ?? $"Deleted {miis[0].Name}";
        }

        var result = await new YesNoWindow().SetMainText(mainText).SetExtraText(t("question.sure_delete.extra")).AwaitAnswer();
        if (!result)
            return;

        foreach (var mii in miis)
        {
            MiiDbService.Remove(mii.MiiId);
        }

        ReloadMiiList();
        ViewUtils.ShowSnackbar(successMessage);
    }

    private async void EditMii(Mii mii)
    {
        var window = new MiiEditorWindow().SetMii(mii);
        var save = await window.AwaitAnswer();
        if (!save)
            return;

        var result = MiiDbService.Update(window.Mii);
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_update", result.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return;
        }

        ReloadMiiList();
    }

    private async void CreateNewMii()
    {
        Mii? mii = null;
        await new OptionsWindow()
            .AddOption("Dice", t("action.randomize"), () => mii = MiiFactory.CreateRandomMii(Random.Random.Shared))
            .AddOption("PersonMale", t("attribute.mii.gender_male"), () => mii = MiiFactory.CreateDefaultMale())
            .AddOption("PersonFemale", t("attribute.mii.gender_female"), () => mii = MiiFactory.CreateDefaultFemale())
            .AwaitAnswer();
        if (mii == null)
            return;

        var window = new MiiEditorWindow().SetMii(mii);
        var save = await window.AwaitAnswer();
        if (!save)
            return;

        var result = MiiDbService.AddToDatabase(window.Mii, SettingsService.Get<string>(SettingsService.MACADDRESS));
        if (result.IsFailure)
        {
            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_create", result.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return;
        }

        ReloadMiiList();
    }

    private void DuplicateMii(Mii[] miis)
    {
        //assuming the mac address is already set correctly
        var macAddress = SettingsService.Get<string>(SettingsService.MACADDRESS);
        foreach (var mii in miis)
        {
            var result = MiiDbService.AddToDatabase(mii, macAddress);
            if (!result.IsFailure)
                continue;

            ViewUtils.ShowSnackbar(t("snackbar_error.mii_failure_duplicate", result.Error.Message)!, ViewUtils.SnackbarType.Danger);
            return;
        }

        var successMessage = t("snackbar_success.created_duplicates_miis", miis.Length)!;
        if (miis.Length == 1)
            successMessage = t("snackbar_success.created_duplicate", miis[0].Name)!;

        ReloadMiiList();
        ViewUtils.ShowSnackbar(successMessage);
    }

    private Mii[] GetSelectedMiis()
    {
        return _miiEntries.Where(entry => entry is { IsSelected: true, Mii: not null }).Select(entry => entry.Mii!).ToArray();
    }

    private void ChangeTopButtons()
    {
        var selectedMiis = GetSelectedMiis();

        if (selectedMiis.Length == 0)
        {
            DeleteMiisButton.IsVisible = false;
            ExportMiisButton.IsVisible = false;
            EditMiisButton.IsVisible = false;
            DuplicateMiisButton.IsVisible = false;
            ImportMiiButton.IsVisible = true;
            SearchSavedMiiButton.IsVisible = true;
            FavoriteMiiButton.IsVisible = false;
            return;
        }

        FavoriteMiiButton.IsVisible = true;
        EditMiisButton.IsVisible = selectedMiis.Length == 1;
        ImportMiiButton.IsVisible = false;
        SearchSavedMiiButton.IsVisible = false;
        DeleteMiisButton.IsVisible = true;
        ExportMiisButton.IsVisible = true;
        DuplicateMiisButton.IsVisible = true;

        FavoriteMiiButton.Classes.Remove("UnFav");
        if (selectedMiis.All(mii => mii.IsFavorite))
            FavoriteMiiButton.Classes.Add("UnFav");
    }

    private void ContextAction(Mii mii, Action<Mii[]> command)
    {
        var selectedMiis = GetSelectedMiis();
        // If the user right clicks and perform action on a selected Mii, that action applies for all the selected Miis
        // But if the user right clicks and perform actions on a mii that is not selected, than it only happens for that specific mii.
        command.Invoke(selectedMiis.Contains(mii) ? selectedMiis : [mii]);
    }

    public sealed class MiiListRow(List<MiiListEntry> items)
    {
        public IReadOnlyList<MiiListEntry> Items { get; } = items;
    }

    public sealed class MiiListEntry(Mii? mii, bool isAddEntry = false) : INotifyPropertyChanged
    {
        private bool _isSelected;

        public Mii? Mii { get; } = mii;
        public bool IsAddEntry { get; } = isAddEntry;
        public bool HasMii => Mii != null;
        public string SelectionGroup { get; } = Guid.NewGuid().ToString("N");
        public string FavoriteActionHeader => Mii?.IsFavorite == true ? t("action.unfavorite") : t("action.favorite");

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public static MiiListEntry CreateAddEntry() => new(null, true);

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
