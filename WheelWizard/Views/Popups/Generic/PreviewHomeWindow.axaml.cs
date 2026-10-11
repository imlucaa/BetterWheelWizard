using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using WheelWizard.Themes;
using WheelWizard.Views.Pages;
using WheelWizard.Views.Popups.Base;

namespace WheelWizard.Views.Popups.Generic;

public partial class PreviewHomeWindow : PopupContent
{
    public bool IsOpen { get; private set; } = true;
    private bool _hideHomeTitle;

    public PreviewHomeWindow(
        LauncherThemeFile theme,
        LauncherBackgroundState background,
        double surfaceOpacity,
        bool transparentLayout,
        bool hideHomeTitle
    )
        : base(true, false, true, t("popup_title.launcher_preview"))
    {
        InitializeComponent();
        UpdatePreview(theme, background, surfaceOpacity, transparentLayout, hideHomeTitle);
    }

    public async void UpdatePreview(
        LauncherThemeFile theme,
        LauncherBackgroundState background,
        double surfaceOpacity,
        bool transparentLayout,
        bool hideHomeTitle
    )
    {
        if (!IsOpen)
            return;

        _hideHomeTitle = hideHomeTitle;
        PreviewPageTitle.IsVisible = PreviewPageTitle.Text != "Home" || !_hideHomeTitle;

        var accent = ParseColor(theme.Accent, Colors.DodgerBlue);
        var brand = ParseColor(theme.Branding, accent);
        var text = ParseColor(theme.Text, Colors.White);
        var pageBackground = ParseColor(theme.Background, Colors.Black);
        var surfaceColor = Mix(pageBackground, RelativeLuminance(pageBackground) > 0.45 ? Colors.Black : Colors.White, 0.09);
        var surfaceBrush = new SolidColorBrush(WithOpacity(surfaceColor, surfaceOpacity));

        Resources["PreviewAccentBrush"] = new SolidColorBrush(accent);
        Resources["PreviewBrandBrush"] = new SolidColorBrush(brand);
        Resources["PreviewTextBrush"] = new SolidColorBrush(text);
        Resources["PreviewSurfaceBrush"] = surfaceBrush;
        Resources["PreviewCardBrush"] = surfaceBrush;

        PreviewBase.Background = new SolidColorBrush(pageBackground);
        PreviewTopSurface.Background = transparentLayout ? Brushes.Transparent : surfaceBrush;
        PreviewSidebarSurface.Background = transparentLayout ? Brushes.Transparent : surfaceBrush;
        PreviewPageSurface.Background = transparentLayout ? Brushes.Transparent : surfaceBrush;
        PreviewScrim.Background = Brushes.Transparent;

        try
        {
            await PreviewBackground.LoadAsync(background);
        }
        catch
        {
            PreviewBackground.IsVisible = false;
        }
    }

    private void PreviewTab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string page })
            return;

        PreviewPageTitle.Text = page;
        PreviewPageTitle.IsVisible = page != "Home" || !_hideHomeTitle;
        PreviewPageSubtitle.Text = page switch
        {
            "Home" => "Launcher preview",
            "Themes" => "Choose, create, and share launcher colors",
            "Rooms" => "Browse active Retro Rewind rooms",
            "Leaderboard" => "Ranked players and placements",
            _ => $"Preview the {page.ToLowerInvariant()} page",
        };
        HomePreview.IsVisible = page == "Home";
        ThemesPreview.IsVisible = page == "Themes";
        ListPreview.IsVisible = page is not ("Home" or "Themes");
        ListItemOne.Text = page switch
        {
            "My profiles" => "Primary profile",
            "Patches" => "Example patch",
            "My Miis" => "Favorite Mii",
            "Rooms" => "Worldwide room",
            "Leaderboard" => "1. Example Player",
            "Friends" => "Online friend",
            _ => "Example item",
        };
        ListItemTwo.Text = page switch
        {
            "Leaderboard" => "2. Another Player",
            "Rooms" => "Regional room",
            _ => "Another item",
        };
    }

    protected override void BeforeClose()
    {
        IsOpen = false;
        PreviewBackground.Dispose();
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            return Color.Parse(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    private static Color Mix(Color start, Color end, double amount) =>
        Color.FromRgb(
            (byte)Math.Round(start.R + ((end.R - start.R) * amount)),
            (byte)Math.Round(start.G + ((end.G - start.G) * amount)),
            (byte)Math.Round(start.B + ((end.B - start.B) * amount))
        );

    private static double RelativeLuminance(Color color) => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
}
