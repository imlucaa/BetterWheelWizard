using Avalonia;
using Avalonia.Media;
using WheelWizard.Settings;

namespace WheelWizard.Themes;

public interface ILauncherThemeService
{
    void Initialize();
}

public sealed class LauncherThemeService(ISettingsManager settingsManager, ISettingsSignalBus settingsSignalBus) : ILauncherThemeService
{
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

        var surfaceScale = CreateSurfaceScale(backgroundColor);
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
    }

    private void OnSettingChanged(SettingChangedSignal signal)
    {
        if (
            signal.Setting == settingsManager.LAUNCHER_THEME_COLOR
            || signal.Setting == settingsManager.LAUNCHER_TEXT_COLOR
            || signal.Setting == settingsManager.LAUNCHER_BODY_TEXT_COLOR
            || signal.Setting == settingsManager.LAUNCHER_BACKGROUND_COLOR
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
}

public static class LauncherThemeExtensions
{
    public static IServiceCollection AddLauncherThemes(this IServiceCollection services)
    {
        services.AddSingleton<ILauncherThemeService, LauncherThemeService>();
        return services;
    }
}
