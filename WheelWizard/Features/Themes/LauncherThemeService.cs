using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using WheelWizard.Services;
using WheelWizard.Settings;

namespace WheelWizard.Themes;

public interface ILauncherThemeService
{
    void Initialize();
}

public sealed class LauncherThemeService(ISettingsManager settingsManager, ISettingsSignalBus settingsSignalBus) : ILauncherThemeService
{
    private sealed class FileFontCollection : FontCollectionBase
    {
        public FileFontCollection(Uri key, string path)
        {
            Key = key;
            using var stream = File.OpenRead(path);
            if (!TryAddGlyphTypeface(stream, out _))
                throw new InvalidDataException("The custom font could not be loaded by the launcher.");
        }

        public override Uri Key { get; }
    }

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

    private static readonly string[] SurfaceResourceKeys = ["Neutral600", "Neutral700", "Neutral800", "Neutral900", "Neutral950"];
    private static readonly string[] TextResourceKeys = ["Neutral50", "Neutral100", "Neutral200", "Neutral300", "Neutral400", "Neutral500"];
    private IDisposable? _subscription;
    private static readonly Dictionary<string, Uri> RegisteredFontCollections = new(StringComparer.OrdinalIgnoreCase);

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
            resources[AccentResourceKeys[i]] = accentScale[i];

        var hasBackground = !string.IsNullOrWhiteSpace(settingsManager.Get<string>(settingsManager.LAUNCHER_BACKGROUND_FILE));
        var surfaceOpacity = hasBackground ? settingsManager.Get<double>(settingsManager.LAUNCHER_SURFACE_OPACITY) : 1.0;
        var surfaceScale = CreateSurfaceScale(backgroundColor).Select(color => WithOpacity(color, surfaceOpacity)).ToArray();
        for (var i = 0; i < SurfaceResourceKeys.Length; i++)
            resources[SurfaceResourceKeys[i]] = surfaceScale[i];

        var readableTextColor = CreateReadableText(textColor, surfaceScale[^1]);
        var textScale = CreateTextScale(readableTextColor, surfaceScale[^1]);
        for (var i = 0; i < TextResourceKeys.Length; i++)
            resources[TextResourceKeys[i]] = textScale[i];

        resources["BodyTextColor"] = new SolidColorBrush(readableTextColor);
        resources["TitleTextColor"] = new SolidColorBrush(readableTextColor);
        resources["TitleIconColor"] = new SolidColorBrush(textScale[2]);
        resources["TitleTextHoverColor"] = new SolidColorBrush(textScale[0]);
        resources["FormFieldLabelColor"] = new SolidColorBrush(textScale[0]);
        resources["LauncherBrandColor"] = new SolidColorBrush(brandColor);
        // Always keep an opaque color beneath the custom image. Theme surfaces may be
        // translucent, but they must reveal the image/base rather than the user's desktop.
        resources["LauncherBaseBackground"] = new SolidColorBrush(backgroundColor);
        resources["LauncherSurfaceBackground"] = new SolidColorBrush(surfaceScale[3]);
        // Without an image there is nothing to reveal through translucent launcher
        // chrome, so preserve the user's exact background color (including pure black).
        // With an image, the normal layout uses card opacity over that image.
        resources["LauncherLayoutBackground"] = new SolidColorBrush(hasBackground ? surfaceScale[3] : backgroundColor);
        // Keep the selected image visible behind non-Home pages while providing a
        // consistent readability layer that is stronger than individual cards.
        resources["LauncherPageBackground"] = new SolidColorBrush(WithOpacity(backgroundColor, hasBackground ? 0.86 : 1.0));
        // Dialogs must remain readable over detailed/bright custom backgrounds. Unlike
        // launcher cards, popup chrome deliberately ignores the user-selected surface opacity.
        resources["PopupBackgroundColor"] = new SolidColorBrush(backgroundColor);
        resources["PopupFrameColor"] = new SolidColorBrush(WithOpacity(surfaceScale[3], 1.0));

        // Sidebar counters sit directly over arbitrary background images in clean-sidebar
        // mode. Keep their badge opaque and calculate text against the badge itself so an
        // almost-white or almost-black image cannot wash the numbers out.
        var (counterBackground, counterForeground) = CreateCounterPalette(backgroundColor, textColor);
        resources["SidebarCounterBackground"] = new SolidColorBrush(counterBackground);
        resources["SidebarCounterForeground"] = new SolidColorBrush(counterForeground);

        var trailOpacity = hasBackground ? surfaceOpacity : 1.0;
        resources["LauncherTrail400"] = new SolidColorBrush(WithOpacity(accentScale[3], trailOpacity));
        resources["LauncherTrail600"] = new SolidColorBrush(WithOpacity(accentScale[5], trailOpacity));
        resources["LauncherTrail200"] = new SolidColorBrush(WithOpacity(accentScale[2], trailOpacity));
        resources["LauncherTrail700"] = new SolidColorBrush(WithOpacity(accentScale[6], trailOpacity));
        resources["LauncherTrail800"] = new SolidColorBrush(WithOpacity(accentScale[7], trailOpacity));
        resources["DefaultFont"] = ResolveFont(settingsManager.Get<string>(settingsManager.LAUNCHER_FONT));
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

    internal static IReadOnlyList<Color> CreateAccentScale(Color color)
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

    internal static Color CreateReadableText(Color color, Color background)
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

    internal static bool HasReadableContrast(Color color, Color background) => ContrastRatio(color, background) >= 4.5;

    internal static (Color Background, Color Foreground) CreateCounterPalette(Color background, Color preferredText)
    {
        var target = RelativeLuminance(background) > 0.45 ? Colors.Black : Colors.White;
        var counterBackground = Mix(background, target, 0.72);
        return (counterBackground, CreateReadableText(preferredText, counterBackground));
    }

    private static IReadOnlyList<Color> CreateTextScale(Color color, Color background) =>
        [
            Mix(color, Colors.White, 0.18),
            Mix(color, Colors.White, 0.09),
            color,
            Mix(color, background, 0.12),
            Mix(color, background, 0.25),
            Mix(color, background, 0.40),
        ];

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

    private static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(byte.MaxValue * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    internal static FontFamily ResolveFont(string value)
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
                var path = Path.Combine(PathManager.WheelWizardAppdataPath, "Appearance", "Fonts", value);
                if (File.Exists(path))
                {
                    using var typeface = SkiaSharp.SKTypeface.FromFile(path);
                    var familyName = typeface?.FamilyName;
                    if (!string.IsNullOrWhiteSpace(familyName))
                    {
                        if (!RegisteredFontCollections.TryGetValue(path, out var collectionKey))
                        {
                            collectionKey = new Uri($"fonts:custom/{Guid.NewGuid():N}");
                            FontManager.Current.AddFontCollection(new FileFontCollection(collectionKey, path));
                            RegisteredFontCollections[path] = collectionKey;
                        }
                        return FontFamily.Parse(
                            $"{collectionKey}#{familyName}, "
                                + "avares://BetterWheelWizard/Resources/Fonts/Roboto-Regular.ttf#Roboto, "
                                + "avares://BetterWheelWizard/Resources/Fonts/ctmkf.ttf#CTMKF"
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
