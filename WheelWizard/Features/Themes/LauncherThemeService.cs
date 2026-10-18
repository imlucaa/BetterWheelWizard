using System.IO.Abstractions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using WheelWizard.ApplicationData;
using WheelWizard.Settings;

namespace WheelWizard.Themes;

public interface ILauncherThemeService
{
    void Initialize();
}

public sealed class LauncherThemeService(
    ISettingsManager settingsManager,
    ISettingsSignalBus settingsSignalBus,
    IApplicationDataLocation applicationData
) : ILauncherThemeService
{
    private static readonly Dictionary<string, Uri> RegisteredFontCollections = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] AccentResourceKeys =
    [
        "Primary50",
        "Primary100",
        "Primary200",
        "Primary300",
        "Primary400",
        "Primary500",
        "Primary600",
        "Primary700",
        "Primary800",
        "Primary900",
        "Primary950",
    ];

    private static readonly string[] BrandResourceKeys =
    [
        "Brand50",
        "Brand100",
        "Brand200",
        "Brand300",
        "Brand400",
        "Brand500",
        "Brand600",
        "Brand700",
        "Brand800",
        "Brand900",
        "Brand950",
    ];

    public static IReadOnlyList<string> ThemeAccentResourceKeys => [.. AccentResourceKeys, .. BrandResourceKeys];

    private static readonly string[] SurfaceResourceKeys = ["Neutral600", "Neutral700", "Neutral800", "Neutral900", "Neutral950"];
    private static readonly string[] TextResourceKeys = ["Neutral50", "Neutral100", "Neutral200", "Neutral300", "Neutral400", "Neutral500"];
    private IDisposable? _subscription;

    public static bool IsValidHexColor(object? value) =>
        value is string text && text.Length == 7 && text[0] == '#' && text.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    public void Initialize()
    {
        ApplyTheme();
        _subscription ??= settingsSignalBus.Subscribe(OnSettingChanged);
    }

    private void ApplyTheme()
    {
        var resources = Application.Current?.Resources;
        if (resources == null)
            return;

        var themeColor = Color.Parse(settingsManager.Get<string>(settingsManager.LAUNCHER_THEME_COLOR));
        var brandColor = Color.Parse(settingsManager.Get<string>(settingsManager.LAUNCHER_TEXT_COLOR));
        var textColor = Color.Parse(settingsManager.Get<string>(settingsManager.LAUNCHER_BODY_TEXT_COLOR));
        var backgroundColor = Color.Parse(settingsManager.Get<string>(settingsManager.LAUNCHER_BACKGROUND_COLOR));
        var accentScale = CreateAccentScale(themeColor);
        for (var i = 0; i < AccentResourceKeys.Length; i++)
        {
            resources[AccentResourceKeys[i]] = accentScale[i];
            resources[BrandResourceKeys[i]] = accentScale[i];
        }

        // Panel opacity is useful with both an image and the solid launcher base.
        // Do not silently discard the user's value when no image is selected.
        var surfaceOpacity = settingsManager.LAUNCHER_SURFACE_OPACITY.Get();
        var surfaceScale = CreateSurfaceScale(backgroundColor).Select(color => ApplyOpacity(color, surfaceOpacity)).ToArray();
        for (var i = 0; i < SurfaceResourceKeys.Length; i++)
            resources[SurfaceResourceKeys[i]] = surfaceScale[i];
        // Explicit brushes avoid Avalonia converting a dynamic Color resource to an
        // opaque brush when it is assigned to a Control.Background property.
        resources["LauncherSurfaceBrush"] = new SolidColorBrush(surfaceScale[^1]);
        resources["LauncherPanelBrush"] = new SolidColorBrush(surfaceScale[3]);
        // Clean sidebar controls presentation only. Panel opacity remains the
        // single source of truth for both content and sidebar transparency.
        resources["LauncherSidebarBrush"] = new SolidColorBrush(ApplyOpacity(backgroundColor, surfaceOpacity));
        // The root window must remain opaque when no custom image is selected. The
        // image control is layered above this fallback and below launcher panels.
        resources["LauncherBaseBrush"] = new SolidColorBrush(Color.FromRgb(backgroundColor.R, backgroundColor.G, backgroundColor.B));
        // Popup chrome must stay opaque and readable. Launcher panel opacity is
        // only for the main window over its custom background image.
        var opaqueSurfaceScale = CreateSurfaceScale(backgroundColor);
        resources["PopupBackgroundBrush"] = new SolidColorBrush(backgroundColor);
        resources["PopupSurfaceBrush"] = new SolidColorBrush(opaqueSurfaceScale[3]);
        resources["PopupBorderBrush"] = new SolidColorBrush(opaqueSurfaceScale[1]);
        resources["LauncherReadableSurfaceBrush"] = new SolidColorBrush(CreateReadableSurface(backgroundColor, surfaceOpacity));
        resources["SidebarBoxBrush"] = new SolidColorBrush(CreateSidebarBoxSurface(backgroundColor, surfaceOpacity, false));
        resources["SidebarBoxHoverBrush"] = new SolidColorBrush(CreateSidebarBoxSurface(backgroundColor, surfaceOpacity, true));

        var accentText = CreateAccentText(themeColor);
        resources["LauncherAccentTextBrush"] = new SolidColorBrush(accentText);

        // The chosen text color is used exactly as picked, whether or not it contrasts with the background.
        var readableTextColor = textColor;
        var textScale = CreateTextScale(readableTextColor, surfaceScale[^1]);
        for (var i = 0; i < TextResourceKeys.Length; i++)
            resources[TextResourceKeys[i]] = textScale[i];

        resources["BodyTextColor"] = new SolidColorBrush(readableTextColor);
        resources["TitleTextColor"] = new SolidColorBrush(readableTextColor);
        resources["TitleIconColor"] = new SolidColorBrush(textScale[2]);
        resources["TitleTextHoverColor"] = new SolidColorBrush(textScale[0]);
        resources["FormFieldLabelColor"] = new SolidColorBrush(textScale[0]);

        // The default scroll bar is a dark gray that reads as a black line over translucent panels.
        // A soft tint of the text color stays visible on any panel opacity and follows the theme.
        resources["ScrollBarThumbFill"] = new SolidColorBrush(ApplyOpacity(textScale[2], 0.3));
        resources["ScrollBarThumbFillPointerOver"] = new SolidColorBrush(ApplyOpacity(textScale[2], 0.5));
        resources["ScrollBarThumbFillPressed"] = new SolidColorBrush(ApplyOpacity(textScale[2], 0.65));
        resources["ScrollBarTrackFill"] = Brushes.Transparent;
        resources["ScrollBarTrackFillPointerOver"] = Brushes.Transparent;
        resources["ScrollBarTrackStroke"] = Brushes.Transparent;
        resources["ScrollBarTrackStrokePointerOver"] = Brushes.Transparent;
        resources["LauncherBrandColor"] = new SolidColorBrush(brandColor);
        resources["LauncherFooterIconBrush"] = new SolidColorBrush(CreateReadableText(Colors.White, backgroundColor));
        resources["DefaultFont"] = ResolveFont(settingsManager.LAUNCHER_FONT.Get(), applicationData.DirectoryPath);
    }

    private void OnSettingChanged(SettingChangedSignal signal)
    {
        if (
            signal.Setting == settingsManager.LAUNCHER_THEME_COLOR
            || signal.Setting == settingsManager.LAUNCHER_TEXT_COLOR
            || signal.Setting == settingsManager.LAUNCHER_BODY_TEXT_COLOR
            || signal.Setting == settingsManager.LAUNCHER_BACKGROUND_COLOR
            || signal.Setting == settingsManager.LAUNCHER_BACKGROUND_FILE
            || signal.Setting == settingsManager.LAUNCHER_SURFACE_OPACITY
            || signal.Setting == settingsManager.LAUNCHER_FONT
        )
            ApplyTheme();
    }

    public static IReadOnlyList<Color> CreateAccentScale(Color color)
    {
        var accent = color;
        return
        [
            Mix(accent, Colors.White, 0.90),
            Mix(accent, Colors.White, 0.76),
            Mix(accent, Colors.White, 0.55),
            Mix(accent, Colors.White, 0.25),
            accent,
            Mix(accent, Colors.Black, 0.12),
            Mix(accent, Colors.Black, 0.24),
            Mix(accent, Colors.Black, 0.38),
            Mix(accent, Colors.Black, 0.52),
            Mix(accent, Colors.Black, 0.64),
            Mix(accent, Colors.Black, 0.78),
        ];
    }

    private static IReadOnlyList<Color> CreateSurfaceScale(Color color)
    {
        var background = color;
        var surfaceTarget = RelativeLuminance(background) > 0.45 ? Colors.Black : Colors.White;
        return
        [
            Mix(background, surfaceTarget, 0.34),
            Mix(background, surfaceTarget, 0.25),
            Mix(background, surfaceTarget, 0.17),
            Mix(background, surfaceTarget, 0.09),
            background,
        ];
    }

    public static Color CreateReadableSurface(Color background, double panelOpacity)
    {
        // Readability-sensitive cards follow panel opacity while retaining the old
        // launcher's subtle 35% tint at zero. Use the same neutral shade as default
        // buttons so controls and cards form one muted, translucent surface family.
        var effectiveOpacity = 0.35 + Math.Clamp(panelOpacity, 0, 1) * 0.65;
        return ApplyOpacity(CreateSurfaceScale(background)[1], effectiveOpacity);
    }

    public static Color CreateSidebarBoxSurface(Color background, double panelOpacity, bool hover)
    {
        var scale = CreateSurfaceScale(background);
        return ApplyOpacity(scale[hover ? 2 : 3], panelOpacity);
    }

    public static Color CreateReadableText(Color color, Color background)
    {
        if (ContrastRatio(color, background) >= 4.5)
            return color;

        var target = ContrastRatio(Colors.White, background) >= ContrastRatio(Colors.Black, background) ? Colors.White : Colors.Black;
        for (var amount = 0.05; amount <= 1; amount += 0.05)
        {
            var candidate = Mix(color, target, amount);
            if (ContrastRatio(candidate, background) >= 4.5)
                return candidate;
        }
        return target;
    }

    // Text on an accent-filled control (Save, Play, ...). Mixing white toward black until it just passes the
    // contrast check produces a washed-out gray on light accents, so use crisp white when it reads well on the
    // accent (3.5:1, the usual bar for bold button labels) and crisp black otherwise.
    public static Color CreateAccentText(Color accent) => ContrastRatio(Colors.White, accent) >= 3.5 ? Colors.White : Colors.Black;

    public static bool HasReadableContrast(Color color, Color background) => ContrastRatio(color, background) >= 4.5;

    public static IReadOnlyList<Color> CreateTextScale(Color color, Color background) =>
        [
            Mix(color, Colors.White, 0.18),
            Mix(color, Colors.White, 0.09),
            color,
            Mix(color, background, 0.12),
            color,
            Mix(color, background, 0.40),
        ];

    public static FontFamily ResolveFont(string value, string dataDirectory)
    {
        try
        {
            if (value.Equals("Inter", StringComparison.OrdinalIgnoreCase))
                return new FontFamily("Inter");
            if (value.Equals("CTMKF", StringComparison.OrdinalIgnoreCase))
                return FontFamily.Parse(
                    "avares://BetterWheelWizard/Resources/Fonts/ctmkf.ttf#CTMKF, avares://BetterWheelWizard/Resources/Fonts/Roboto-Regular.ttf#Roboto"
                );
            if (Path.GetExtension(value).ToLowerInvariant() is ".ttf" or ".otf")
            {
                var path = Path.Combine(dataDirectory, "Appearance", "Fonts", Path.GetFileName(value));
                if (File.Exists(path))
                {
                    using var typeface = SkiaSharp.SKTypeface.FromFile(path);
                    if (!string.IsNullOrWhiteSpace(typeface?.FamilyName))
                    {
                        if (!RegisteredFontCollections.TryGetValue(path, out var key))
                        {
                            key = new Uri($"fonts:custom/{Guid.NewGuid():N}");
                            FontManager.Current.AddFontCollection(new FileFontCollection(key, path));
                            RegisteredFontCollections[path] = key;
                        }
                        return FontFamily.Parse(
                            $"{key}#{typeface.FamilyName}, avares://BetterWheelWizard/Resources/Fonts/Roboto-Regular.ttf#Roboto"
                        );
                    }
                }
            }
        }
        catch (ArgumentException) { }
        catch (IOException) { }
        return FontFamily.Parse(
            "avares://BetterWheelWizard/Resources/Fonts/Roboto-Regular.ttf#Roboto, avares://BetterWheelWizard/Resources/Fonts/ctmkf.ttf#CTMKF"
        );
    }

    private sealed class FileFontCollection : FontCollectionBase
    {
        public FileFontCollection(Uri key, string path)
        {
            Key = key;
            using var stream = File.OpenRead(path);
            TryAddGlyphTypeface(stream, out _);
        }

        public override Uri Key { get; }
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static Color Mix(Color start, Color end, double amount)
    {
        static byte Channel(byte start, byte end, double amount) => (byte)Math.Round(start + ((end - start) * amount));
        return Color.FromRgb(Channel(start.R, end.R, amount), Channel(start.G, end.G, amount), Channel(start.B, end.B, amount));
    }

    public static Color ApplyOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(byte.MaxValue * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);
}

public static class LauncherThemeExtensions
{
    public static IServiceCollection AddLauncherThemes(this IServiceCollection services)
    {
        services.AddSingleton<ILauncherThemeService, LauncherThemeService>();
        services.AddSingleton<ILauncherBackgroundService, LauncherBackgroundService>();
        return services;
    }
}
