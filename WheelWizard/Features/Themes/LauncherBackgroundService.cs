using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using WheelWizard.Services;
using WheelWizard.Settings;

namespace WheelWizard.Themes;

public sealed record LauncherBackgroundState(string? Path, Stretch Stretch, double Opacity, bool AnimationsEnabled);

public interface ILauncherBackgroundService
{
    event Action<LauncherBackgroundState>? Changed;
    LauncherBackgroundState Current { get; }
    void Initialize();
    Task<string> ImportFileAsync(string sourcePath);
    Task<string> ImportUrlAsync(string url);
    void Remove();
}

public sealed class LauncherBackgroundService(ISettingsManager settingsManager, ISettingsSignalBus settingsSignalBus)
    : ILauncherBackgroundService
{
    private const long MaxDownloadBytes = 25 * 1024 * 1024;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private IDisposable? _subscription;

    public event Action<LauncherBackgroundState>? Changed;

    public static string BackgroundsFolderPath => Path.Combine(PathManager.WheelWizardAppdataPath, "Appearance", "Backgrounds");

    public LauncherBackgroundState Current
    {
        get
        {
            var fileName = settingsManager.Get<string>(settingsManager.LAUNCHER_BACKGROUND_FILE);
            var path = string.IsNullOrWhiteSpace(fileName) ? null : Path.Combine(BackgroundsFolderPath, Path.GetFileName(fileName));
            if (path != null && !File.Exists(path))
                path = null;

            var stretch = Enum.TryParse<Stretch>(settingsManager.Get<string>(settingsManager.LAUNCHER_BACKGROUND_STRETCH), out var parsed)
                ? parsed
                : Stretch.UniformToFill;
            return new(
                path,
                stretch,
                settingsManager.Get<double>(settingsManager.LAUNCHER_BACKGROUND_OPACITY),
                settingsManager.Get<bool>(settingsManager.ENABLE_ANIMATIONS)
            );
        }
    }

    public void Initialize()
    {
        Directory.CreateDirectory(BackgroundsFolderPath);
        _subscription ??= settingsSignalBus.Subscribe(OnSettingChanged);
    }

    public async Task<string> ImportFileAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The selected image no longer exists.", sourcePath);
        if (new FileInfo(sourcePath).Length > MaxDownloadBytes)
            throw new InvalidDataException("The image is larger than the 25 MB limit.");

        await using var source = File.OpenRead(sourcePath);
        return await ImportStreamAsync(source, Path.GetExtension(sourcePath));
    }

    public async Task<string> ImportUrlAsync(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidDataException("Enter a valid HTTP or HTTPS image URL.");

        using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            throw new InvalidDataException("The image is larger than the 25 MB limit.");

        await using var network = await response.Content.ReadAsStreamAsync();
        await using var limited = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await network.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > MaxDownloadBytes)
                throw new InvalidDataException("The image is larger than the 25 MB limit.");
            await limited.WriteAsync(buffer.AsMemory(0, read));
        }
        limited.Position = 0;
        return await ImportStreamAsync(limited, Path.GetExtension(uri.AbsolutePath));
    }

    public void Remove()
    {
        var fileName = settingsManager.Get<string>(settingsManager.LAUNCHER_BACKGROUND_FILE);
        settingsManager.Set(settingsManager.LAUNCHER_BACKGROUND_FILE, string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            File.Delete(Path.Combine(BackgroundsFolderPath, Path.GetFileName(fileName)));
        }
        catch { }
    }

    private async Task<string> ImportStreamAsync(Stream source, string suggestedExtension)
    {
        Directory.CreateDirectory(BackgroundsFolderPath);
        await using var memory = new MemoryStream();
        await source.CopyToAsync(memory);
        memory.Position = 0;
        using var codec = SKCodec.Create(memory) ?? throw new InvalidDataException("This file is not a supported image.");
        var extension = GetExtension(codec.EncodedFormat, suggestedExtension);
        var fileName = $"background-{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(BackgroundsFolderPath, fileName);
        memory.Position = 0;
        await using (var output = File.Create(destination))
            await memory.CopyToAsync(output);

        var oldFile = settingsManager.Get<string>(settingsManager.LAUNCHER_BACKGROUND_FILE);
        settingsManager.Set(settingsManager.LAUNCHER_BACKGROUND_FILE, fileName);
        if (!string.IsNullOrWhiteSpace(oldFile) && !string.Equals(oldFile, fileName, StringComparison.OrdinalIgnoreCase))
        {
            var oldPath = Path.Combine(BackgroundsFolderPath, Path.GetFileName(oldFile));
            try
            {
                File.Delete(oldPath);
            }
            catch { }
        }
        return fileName;
    }

    private static string GetExtension(SKEncodedImageFormat format, string suggestedExtension) =>
        format switch
        {
            SKEncodedImageFormat.Gif => ".gif",
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Jpeg => ".jpg",
            SKEncodedImageFormat.Webp => ".webp",
            SKEncodedImageFormat.Bmp => ".bmp",
            SKEncodedImageFormat.Ico => ".ico",
            _ => string.IsNullOrWhiteSpace(suggestedExtension) ? ".image" : suggestedExtension.ToLowerInvariant(),
        };

    private void OnSettingChanged(SettingChangedSignal signal)
    {
        if (
            signal.Setting == settingsManager.LAUNCHER_BACKGROUND_FILE
            || signal.Setting == settingsManager.LAUNCHER_BACKGROUND_STRETCH
            || signal.Setting == settingsManager.LAUNCHER_BACKGROUND_OPACITY
            || signal.Setting == settingsManager.ENABLE_ANIMATIONS
        )
            Changed?.Invoke(Current);
    }
}

public sealed class LauncherBackgroundImage : Avalonia.Controls.Image, IDisposable
{
    private readonly List<Bitmap> _frames = [];
    private DispatcherTimer? _timer;
    private int _frameIndex;
    private int[] _durations = [];
    private string? _loadedPath;

    public async Task LoadAsync(LauncherBackgroundState state)
    {
        Stretch = state.Stretch;
        Opacity = state.Opacity;
        IsVisible = state.Path != null;
        if (string.Equals(_loadedPath, state.Path, StringComparison.OrdinalIgnoreCase) && _frames.Count > 0)
        {
            if (_frames.Count > 1 && _timer == null)
                ScheduleNextFrame();
            return;
        }

        DisposeFrames();
        _loadedPath = state.Path;
        if (state.Path == null)
            return;

        var decoded = await Task.Run(() => Decode(state.Path));
        foreach (var frame in decoded.Frames)
            _frames.Add(frame);
        _durations = decoded.Durations;
        Source = _frames[0];
        // Background animation is independent of wheel interaction animation.
        if (_frames.Count > 1)
            ScheduleNextFrame();
    }

    internal static (List<Bitmap> Frames, int[] Durations) Decode(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("The background image could not be decoded.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 4096 || info.Height > 4096)
            throw new InvalidDataException("The background dimensions must be between 1 and 4096 pixels.");
        var frameInfo = codec.FrameInfo;
        var frameCount = Math.Max(1, frameInfo.Length);
        var decodedBytes = (long)info.Width * info.Height * 4 * frameCount;
        if (frameCount > 300 || decodedBytes > 256L * 1024 * 1024)
            throw new InvalidDataException("This animated image has too many or too-large frames.");

        var frames = new List<Bitmap>(frameCount);
        var durations = new int[frameCount];
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        for (var i = 0; i < frameCount; i++)
        {
            var requiredFrame = frameInfo.Length > i ? frameInfo[i].RequiredFrame : -1;
            var options = new SKCodecOptions(i, requiredFrame);
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, options);
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                throw new InvalidDataException("A frame in the animated image could not be decoded.");
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = encoded.AsStream();
            frames.Add(new Bitmap(stream));
            durations[i] = frameInfo.Length > i ? Math.Max(20, frameInfo[i].Duration) : 100;
        }
        return (frames, durations);
    }

    internal static int GetAnimatedFrameCount(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("The background image could not be decoded.");
        return Math.Max(1, codec.FrameInfo.Length);
    }

    private void ScheduleNextFrame()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_durations[_frameIndex]) };
        _timer.Tick += (_, _) =>
        {
            _timer?.Stop();
            _frameIndex = (_frameIndex + 1) % _frames.Count;
            Source = _frames[_frameIndex];
            ScheduleNextFrame();
        };
        _timer.Start();
    }

    private void DisposeFrames()
    {
        _timer?.Stop();
        _timer = null;
        Source = null;
        foreach (var frame in _frames)
            frame.Dispose();
        _frames.Clear();
        _frameIndex = 0;
        _loadedPath = null;
    }

    public void Dispose() => DisposeFrames();
}
