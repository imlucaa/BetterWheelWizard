using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using WheelWizard.Settings;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Themes;

namespace WheelWizard.Views.Pages;

public partial class ThemesPage : UserControlBase
{
    [Inject]
    private ISettingsManager SettingsService { get; set; } = null!;

    public ThemesPage()
    {
        InitializeComponent();
        ColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_THEME_COLOR);
        BrandColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_TEXT_COLOR);
        TextColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_BODY_TEXT_COLOR);
        BackgroundColorTextBox.Text = SettingsService.Get<string>(SettingsService.LAUNCHER_BACKGROUND_COLOR);
        UpdatePreview();
    }

    private void ApplyTheme_OnClick(object? sender, RoutedEventArgs e)
    {
        var color = NormalizeHex(ColorTextBox.Text);
        var brandColor = NormalizeHex(BrandColorTextBox.Text);
        var textColor = NormalizeHex(TextColorTextBox.Text);
        var backgroundColor = NormalizeHex(BackgroundColorTextBox.Text);
        if (
            !LauncherThemeService.IsValidHexColor(color)
            || !LauncherThemeService.IsValidHexColor(brandColor)
            || !LauncherThemeService.IsValidHexColor(textColor)
            || !LauncherThemeService.IsValidHexColor(backgroundColor)
        )
        {
            ColorError.IsVisible = true;
            return;
        }

        ColorError.IsVisible = false;
        ColorTextBox.Text = color;
        BrandColorTextBox.Text = brandColor;
        TextColorTextBox.Text = textColor;
        BackgroundColorTextBox.Text = backgroundColor;
        SettingsService.Set(SettingsService.LAUNCHER_THEME_COLOR, color);
        SettingsService.Set(SettingsService.LAUNCHER_TEXT_COLOR, brandColor);
        SettingsService.Set(SettingsService.LAUNCHER_BODY_TEXT_COLOR, textColor);
        SettingsService.Set(SettingsService.LAUNCHER_BACKGROUND_COLOR, backgroundColor);
        UpdatePreview();
        Dispatcher.UIThread.Post(() => NavigationManager.NavigateTo<ThemesPage>(), DispatcherPriority.Background);
    }

    private void Color_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ColorError.IsVisible = false;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var color = NormalizeHex(ColorTextBox.Text);
        if (LauncherThemeService.IsValidHexColor(color))
            ColorPreview.Background = new SolidColorBrush(Color.Parse(color));

        var textColor = NormalizeHex(TextColorTextBox.Text);
        var backgroundColor = NormalizeHex(BackgroundColorTextBox.Text);
        TextContrastNotice.IsVisible = false;
        if (!LauncherThemeService.IsValidHexColor(textColor) || !LauncherThemeService.IsValidHexColor(backgroundColor))
            return;

        var selected = Color.Parse(textColor);
        var effectiveBackground = LauncherThemeService.CreateDarkBackground(Color.Parse(backgroundColor));
        var effectiveText = LauncherThemeService.CreateReadableText(selected, effectiveBackground);
        EffectiveTextColorPreview.Background = new SolidColorBrush(effectiveText);
        if (effectiveText == selected)
            return;

        var effectiveHex = $"#{effectiveText.R:X2}{effectiveText.G:X2}{effectiveText.B:X2}";
        TextContrastMessage.Text = $"{textColor} is too dark for this background. It will display as {effectiveHex} to keep text readable.";
        TextContrastNotice.IsVisible = true;
    }

    private static string NormalizeHex(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 6 && text[0] != '#')
            text = $"#{text}";
        return text.ToUpperInvariant();
    }
}
