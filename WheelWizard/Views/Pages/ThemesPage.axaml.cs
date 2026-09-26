using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using WheelWizard.Services;
using WheelWizard.Settings;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Themes;
using WheelWizard.Views.Popups.Generic;

namespace WheelWizard.Views.Pages;

public sealed record LauncherThemeFile(string Name, string Accent, string Branding, string Text, string Background)
{
    internal bool HasValidNameAndColors() =>
        !string.IsNullOrWhiteSpace(Name) && new[] { Accent, Branding, Text, Background }.All(LauncherThemeService.IsValidHexColor);
}

internal static class LauncherThemeShareCode
{
    private const string Prefix = "BWW1";

    internal static string Encode(LauncherThemeFile theme) =>
        $"{Prefix}:{WithoutHash(theme.Accent)}:{WithoutHash(theme.Branding)}:{WithoutHash(theme.Text)}:{WithoutHash(theme.Background)}";

    internal static bool TryDecode(string? code, out LauncherThemeFile theme)
    {
        theme = new LauncherThemeFile(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var parts = code.Trim().Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 5 || !string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var colors = parts.Skip(1).Select(value => $"#{WithoutHash(value).ToUpperInvariant()}").ToArray();
        if (!colors.All(LauncherThemeService.IsValidHexColor))
            return false;

        theme = new LauncherThemeFile(string.Empty, colors[0], colors[1], colors[2], colors[3]);
        return true;
    }

    private static string WithoutHash(string value) => value.Trim().TrimStart('#');
}

public partial class ThemesPage : UserControlBase
{
    private sealed record ThemeChoice(string DisplayName, LauncherThemeFile Theme, string? FilePath, bool IsBuiltIn)
    {
        public override string ToString() => DisplayName;
    }

    private static readonly LauncherThemeFile[] BuiltInThemes =
    [
        new("BetterWheelWizard", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#000000"),
        new("Red", "#F04444", "#F04444", "#F8DADA", "#260909"),
        new("Orange", "#FF8A3D", "#FF8A3D", "#FFE0CC", "#281006"),
        new("Yellow", "#FFD80D", "#FFD80D", "#FFF4B8", "#211B02"),
        new("Green", "#25C982", "#25C982", "#D0F5E3", "#062418"),
        new("Cyan", "#20D4E8", "#20D4E8", "#D0F8FC", "#05252A"),
        new("Blue", "#249AF3", "#249AF3", "#DDF2FF", "#071A2D"),
        new("Purple", "#9B6DFF", "#9B6DFF", "#E4D9FF", "#160A2C"),
        new("Pink", "#FF4FA3", "#FF4FA3", "#FFD8EA", "#290817"),
        new("Coral", "#FF6B6B", "#FF6B6B", "#FFE1E1", "#271011"),
        new("Peach", "#FF9F68", "#FF9F68", "#FFE8D8", "#28160C"),
        new("Mint", "#52D6A8", "#52D6A8", "#D9F8ED", "#09251D"),
        new("Emerald", "#10B981", "#10B981", "#D1FAE5", "#05251A"),
        new("Teal", "#14B8A6", "#14B8A6", "#CCFBF1", "#052522"),
        new("Sky", "#38BDF8", "#38BDF8", "#DDF5FF", "#082330"),
        new("Indigo", "#6366F1", "#6366F1", "#E0E1FF", "#11132E"),
        new("Lavender", "#A78BFA", "#A78BFA", "#EDE7FF", "#1B1230"),
        new("Violet", "#8B5CF6", "#8B5CF6", "#E8DEFF", "#170C2D"),
        new("Rose", "#F43F5E", "#F43F5E", "#FFE0E6", "#2A0810"),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static string ThemesFolderPath => Path.Combine(PathManager.WheelWizardAppdataPath, "Themes");
    private static string DeletedBuiltInThemesPath => Path.Combine(ThemesFolderPath, "deleted-built-in-themes.json");
    private readonly List<ThemeChoice> _themeChoices = [];
    private ThemeChoice? _selectedTheme;
    private string _editorThemeName = string.Empty;
    private bool _isLoadingTheme;

    [Inject]
    private ISettingsManager SettingsService { get; set; } = null!;

    public ThemesPage()
    {
        InitializeComponent();
        LoadCurrentTheme();
        ReloadThemeChoices();
        UpdatePreview();
    }

    private void ReloadThemeChoices(string? selectName = null)
    {
        _themeChoices.Clear();
        Directory.CreateDirectory(ThemesFolderPath);
        var deletedBuiltIns = LoadDeletedBuiltInThemes();
        RestorePresetsButton.IsVisible = deletedBuiltIns.Count > 0;
        foreach (var theme in BuiltInThemes.Where(theme => !deletedBuiltIns.Contains(theme.Name)))
            _themeChoices.Add(new ThemeChoice(FormatThemeName(theme, true), theme, null, true));

        foreach (
            var file in Directory.EnumerateFiles(ThemesFolderPath, "*.bwwtheme").OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        )
        {
            var theme = TryReadTheme(file);
            if (theme != null)
                _themeChoices.Add(new ThemeChoice(FormatThemeName(theme, false), theme, file, false));
        }

        if (!string.IsNullOrWhiteSpace(selectName))
            SelectTheme(_themeChoices.LastOrDefault(item => item.Theme.Name == selectName), loadEditor: false);
    }

    private void LoadCurrentTheme()
    {
        ColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_THEME_COLOR);
        BrandColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_TEXT_COLOR);
        TextColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_BODY_TEXT_COLOR);
        BackgroundColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_BACKGROUND_COLOR);
    }

    private async void ChooseTheme_OnClick(object? sender, RoutedEventArgs e)
    {
        var selectedIndex = await new ThemeLibraryWindow(_themeChoices.Select(choice => choice.DisplayName).ToArray()).AwaitAnswer();
        if (selectedIndex is not >= 0 || selectedIndex >= _themeChoices.Count)
            return;

        SelectTheme(_themeChoices[selectedIndex.Value], loadEditor: true);
    }

    private void SelectTheme(ThemeChoice? choice, bool loadEditor)
    {
        _selectedTheme = choice;
        DeleteThemeButton.IsEnabled = choice != null;
        SelectedThemeText.Text = choice?.Theme.Name ?? "Current colors";
        if (choice == null || !loadEditor)
            return;

        SetEditor(choice.Theme);
        ThemeStatusText.Text = $"Loaded {choice.Theme.Name}. Apply to use it.";
    }

    private async void DeleteTheme_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedTheme is not ThemeChoice choice)
            return;

        var confirmed = await new YesNoWindow()
            .SetMainText($"Delete {choice.Theme.Name}?")
            .SetExtraText(choice.IsBuiltIn ? "You can restore this built-in theme later." : "This saved theme file will be removed.")
            .SetButtonText("Delete", "Cancel")
            .SetButtonVariants(
                WheelWizard.Views.Components.Button.ButtonsVariantType.Danger,
                WheelWizard.Views.Components.Button.ButtonsVariantType.Default
            )
            .AwaitAnswer();
        if (!confirmed)
            return;

        if (choice.IsBuiltIn)
        {
            var deletedBuiltIns = LoadDeletedBuiltInThemes();
            deletedBuiltIns.Add(choice.Theme.Name);
            File.WriteAllText(DeletedBuiltInThemesPath, JsonSerializer.Serialize(deletedBuiltIns.Order(), JsonOptions));
        }
        else if (choice.FilePath != null)
        {
            File.Delete(choice.FilePath);
        }
        _selectedTheme = null;
        ReloadThemeChoices();
        DeleteThemeButton.IsEnabled = false;
        SelectedThemeText.Text = "Current colors";
        ThemeStatusText.Text = $"Deleted {choice.Theme.Name}.";
    }

    private async void RestorePresets_OnClick(object? sender, RoutedEventArgs e)
    {
        var confirmed = await new YesNoWindow()
            .SetMainText("Restore all built-in themes?")
            .SetExtraText("Deleted built-in color presets will return to the theme library.")
            .SetButtonText("Restore", "Cancel")
            .SetButtonVariants(
                WheelWizard.Views.Components.Button.ButtonsVariantType.Confirm,
                WheelWizard.Views.Components.Button.ButtonsVariantType.Default
            )
            .AwaitAnswer();
        if (!confirmed)
            return;

        if (File.Exists(DeletedBuiltInThemesPath))
            File.Delete(DeletedBuiltInThemesPath);
        ReloadThemeChoices();
        ThemeStatusText.Text = "Built-in themes restored.";
    }

    private void SetEditor(LauncherThemeFile theme)
    {
        _isLoadingTheme = true;
        try
        {
            _editorThemeName = theme.Name;
            ColorTextBox.Text = theme.Accent;
            BrandColorTextBox.Text = theme.Branding;
            TextColorTextBox.Text = theme.Text;
            BackgroundColorTextBox.Text = theme.Background;
        }
        finally
        {
            _isLoadingTheme = false;
        }
        UpdatePreview();
    }

    private void ApplyTheme_OnClick(object? sender, RoutedEventArgs e)
    {
        var theme = ReadEditor(requireName: false);
        if (theme == null)
            return;
        SettingsService.Set(SettingsService.LAUNCHER_THEME_COLOR, theme.Accent);
        SettingsService.Set(SettingsService.LAUNCHER_TEXT_COLOR, theme.Branding);
        SettingsService.Set(SettingsService.LAUNCHER_BODY_TEXT_COLOR, theme.Text);
        SettingsService.Set(SettingsService.LAUNCHER_BACKGROUND_COLOR, theme.Background);
        var appliedName = string.IsNullOrWhiteSpace(theme.Name) ? "Theme" : theme.Name;
        ThemeStatusText.Text = $"{appliedName} applied.";
        Dispatcher.UIThread.Post(() => NavigationManager.NavigateTo<ThemesPage>(), DispatcherPriority.Background);
    }

    private async void SaveTheme_OnClick(object? sender, RoutedEventArgs e)
    {
        var requestedName = await new TextInputWindow()
            .SetMainText("Save current colors")
            .SetExtraText("Give this theme a name so it appears in your library.")
            .SetPlaceholderText("Theme name")
            .SetInitialText(_editorThemeName)
            .SetButtonText("Cancel", "Save")
            .ShowDialog();
        if (requestedName == null)
            return;

        _editorThemeName = requestedName.Trim();
        var theme = ReadEditor(requireName: true);
        if (theme == null)
            return;
        Directory.CreateDirectory(ThemesFolderPath);
        var existing = FindSavedTheme(theme.Name);
        if (existing != null)
        {
            var replace = await new YesNoWindow()
                .SetMainText($"Replace {theme.Name}?")
                .SetExtraText("A saved theme with this name already exists.")
                .SetButtonText("Replace", "Cancel")
                .SetButtonVariants(
                    WheelWizard.Views.Components.Button.ButtonsVariantType.Confirm,
                    WheelWizard.Views.Components.Button.ButtonsVariantType.Default
                )
                .AwaitAnswer();
            if (!replace)
                return;
        }
        var path = existing ?? Path.Combine(ThemesFolderPath, $"{MakeSafeFileName(theme.Name)}.bwwtheme");
        File.WriteAllText(path, JsonSerializer.Serialize(theme, JsonOptions));
        ReloadThemeChoices(theme.Name);
        SelectedThemeText.Text = theme.Name;
        ThemeStatusText.Text = existing == null ? $"{theme.Name} saved." : $"{theme.Name} updated.";
    }

    private async void CopyThemeCode_OnClick(object? sender, RoutedEventArgs e)
    {
        var theme = ReadEditor(requireName: false);
        if (theme == null)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            ThemeStatusText.Text = "Clipboard is not available.";
            return;
        }

        await clipboard.SetTextAsync(LauncherThemeShareCode.Encode(theme));
        ThemeStatusText.Text = "Theme code copied.";
    }

    private async void PasteThemeCode_OnClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var code = clipboard == null ? null : await clipboard.TryGetTextAsync();
        if (!LauncherThemeShareCode.TryDecode(code, out var theme))
        {
            ThemeStatusText.Text = "Clipboard does not contain a valid BWW theme code.";
            return;
        }

        ColorTextBox.Text = theme.Accent;
        BrandColorTextBox.Text = theme.Branding;
        TextColorTextBox.Text = theme.Text;
        BackgroundColorTextBox.Text = theme.Background;
        _editorThemeName = string.Empty;
        _selectedTheme = null;
        SelectedThemeText.Text = "Imported colors";
        DeleteThemeButton.IsEnabled = false;
        UpdatePreview();
        ThemeStatusText.Text = "Theme code loaded. Apply or save it when ready.";
    }

    private LauncherThemeFile? ReadEditor(bool requireName)
    {
        var name = _editorThemeName.Trim();
        var theme = new LauncherThemeFile(
            name,
            NormalizeHex(ColorTextBox.Text),
            NormalizeHex(BrandColorTextBox.Text),
            NormalizeHex(TextColorTextBox.Text),
            NormalizeHex(BackgroundColorTextBox.Text)
        );
        ColorError.IsVisible = (requireName && string.IsNullOrWhiteSpace(name)) || !IsValidTheme(theme);
        return ColorError.IsVisible ? null : theme;
    }

    private static LauncherThemeFile? TryReadTheme(string path)
    {
        try
        {
            var theme = JsonSerializer.Deserialize<LauncherThemeFile>(File.ReadAllText(path), JsonOptions);
            return theme?.HasValidNameAndColors() == true ? theme : null;
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> LoadDeletedBuiltInThemes()
    {
        try
        {
            if (!File.Exists(DeletedBuiltInThemesPath))
                return new(StringComparer.OrdinalIgnoreCase);
            var names = JsonSerializer.Deserialize<string[]>(File.ReadAllText(DeletedBuiltInThemesPath), JsonOptions) ?? [];
            return new(names, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool IsValidTheme(LauncherThemeFile theme) =>
        new[] { theme.Accent, theme.Branding, theme.Text, theme.Background }.All(LauncherThemeService.IsValidHexColor);

    private string FormatThemeName(LauncherThemeFile theme, bool isBuiltIn)
    {
        var suffix = isBuiltIn ? " (built-in)" : string.Empty;
        return ThemesMatchCurrentSettings(theme) ? $"✓ {theme.Name}{suffix} — Active" : $"{theme.Name}{suffix}";
    }

    private bool ThemesMatchCurrentSettings(LauncherThemeFile theme) =>
        string.Equals(theme.Accent, SettingsService.Get<string>(SettingsService.LAUNCHER_THEME_COLOR), StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            theme.Branding,
            SettingsService.Get<string>(SettingsService.LAUNCHER_TEXT_COLOR),
            StringComparison.OrdinalIgnoreCase
        )
        && string.Equals(
            theme.Text,
            SettingsService.Get<string>(SettingsService.LAUNCHER_BODY_TEXT_COLOR),
            StringComparison.OrdinalIgnoreCase
        )
        && string.Equals(
            theme.Background,
            SettingsService.Get<string>(SettingsService.LAUNCHER_BACKGROUND_COLOR),
            StringComparison.OrdinalIgnoreCase
        );

    private static string? FindSavedTheme(string name) =>
        Directory
            .EnumerateFiles(ThemesFolderPath, "*.bwwtheme")
            .FirstOrDefault(path => string.Equals(TryReadTheme(path)?.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Trim().Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "theme" : safe;
    }

    private void Color_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoadingTheme)
            return;

        ColorError.IsVisible = false;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var color = NormalizeHex(ColorTextBox.Text);
        if (LauncherThemeService.IsValidHexColor(color))
        {
            ColorPreview.Background = new SolidColorBrush(Color.Parse(color));
            UpdateColorChoice(AccentSwatch, color);
        }

        var brandColor = NormalizeHex(BrandColorTextBox.Text);
        if (LauncherThemeService.IsValidHexColor(brandColor))
            UpdateColorChoice(BrandSwatch, brandColor);

        var textColor = NormalizeHex(TextColorTextBox.Text);
        var backgroundColor = NormalizeHex(BackgroundColorTextBox.Text);
        if (LauncherThemeService.IsValidHexColor(textColor))
            UpdateColorChoice(TextSwatch, textColor);
        if (LauncherThemeService.IsValidHexColor(backgroundColor))
            UpdateColorChoice(BackgroundSwatch, backgroundColor);
        TextContrastNotice.IsVisible = false;
        if (!LauncherThemeService.IsValidHexColor(textColor) || !LauncherThemeService.IsValidHexColor(backgroundColor))
            return;

        var selected = Color.Parse(textColor);
        var effective = LauncherThemeService.CreateReadableText(selected, Color.Parse(backgroundColor));
        EffectiveTextColorPreview.Background = new SolidColorBrush(effective);
        if (effective == selected)
            return;
        TextContrastMessage.Text =
            $"{textColor} is too dark for this background. It will display as #{effective.R:X2}{effective.G:X2}{effective.B:X2}.";
        TextContrastNotice.IsVisible = true;
    }

    private async void AccentColor_OnClick(object? sender, RoutedEventArgs e) =>
        ColorTextBox.Text = await PickColor("Main color", ColorTextBox.Text) ?? ColorTextBox.Text;

    private async void BrandColor_OnClick(object? sender, RoutedEventArgs e) =>
        BrandColorTextBox.Text = await PickColor("Wheel and title color", BrandColorTextBox.Text) ?? BrandColorTextBox.Text;

    private async void TextColor_OnClick(object? sender, RoutedEventArgs e) =>
        TextColorTextBox.Text = await PickColor("Text color", TextColorTextBox.Text) ?? TextColorTextBox.Text;

    private async void BackgroundColor_OnClick(object? sender, RoutedEventArgs e) =>
        BackgroundColorTextBox.Text = await PickColor("Background color", BackgroundColorTextBox.Text) ?? BackgroundColorTextBox.Text;

    private static async Task<string?> PickColor(string title, string? currentValue)
    {
        var normalized = NormalizeHex(currentValue);
        var initialColor = LauncherThemeService.IsValidHexColor(normalized) ? Color.Parse(normalized) : Colors.White;
        var selected = await new ThemeColorPickerWindow(title, initialColor).AwaitColor();
        return selected is { } color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : null;
    }

    private static void UpdateColorChoice(Border swatch, string hex)
    {
        swatch.Background = new SolidColorBrush(Color.Parse(hex));
    }

    private static string NormalizeHex(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 6 && text[0] != '#')
            text = $"#{text}";
        return text.ToUpperInvariant();
    }
}
