using System.IO.Abstractions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using WheelWizard.ApplicationData;
using WheelWizard.Settings;

namespace WheelWizard.Themes;

public sealed record LauncherBackgroundState(
    string? Path,
    Stretch Stretch,
    double Opacity,
    double Blur = 0,
    double Zoom = 1,
    double FocusX = 0.5,
    double FocusY = 0.5
)
{
    /// <summary>Zoom and position only apply while the image fills the window; the other fits show it as it is.</summary>
    public bool IsFramed => Stretch == Stretch.UniformToFill && (Zoom != 1 || FocusX != 0.5 || FocusY != 0.5);
}

public interface ILauncherBackgroundService
{
    event Action<LauncherBackgroundState>? Changed;
    LauncherBackgroundState Current { get; }
    Task ImportFileAsync(string sourcePath);
    Task ImportUrlAsync(string url);
    void Remove();
}

public sealed class LauncherBackgroundService : ILauncherBackgroundService, IDisposable
{
    private const long MaxBytes = 25 * 1024 * 1024;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly ISettingsManager _settings;
    private readonly ISettingsSignalBus _signals;
    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly IDisposable _subscription;

    public event Action<LauncherBackgroundState>? Changed;

    public LauncherBackgroundService(
        ISettingsManager settings,
        ISettingsSignalBus signals,
        IFileSystem files,
        IApplicationDataLocation data
    )
    {
        _settings = settings;
        _signals = signals;
        _files = files;
        _folder = files.Path.Combine(data.DirectoryPath, "Appearance", "Backgrounds");
        _files.Directory.CreateDirectory(_folder);
        _subscription = signals.Subscribe(OnSettingChanged);
    }

    public LauncherBackgroundState Current
    {
        get
        {
            var file = _settings.LAUNCHER_BACKGROUND_FILE.Get();
            var path = string.IsNullOrWhiteSpace(file) ? null : _files.Path.Combine(_folder, _files.Path.GetFileName(file));
            if (path != null && !_files.File.Exists(path))
                path = null;
            var stretch = Enum.TryParse<Stretch>(_settings.LAUNCHER_BACKGROUND_STRETCH.Get(), out var parsed)
                ? parsed
                : Stretch.UniformToFill;
            return new(
                path,
                stretch,
                _settings.LAUNCHER_BACKGROUND_OPACITY.Get(),
                _settings.LAUNCHER_BACKGROUND_BLUR.Get(),
                _settings.LAUNCHER_BACKGROUND_ZOOM.Get(),
                _settings.LAUNCHER_BACKGROUND_FOCUS_X.Get(),
                _settings.LAUNCHER_BACKGROUND_FOCUS_Y.Get()
            );
        }
    }

    public async Task ImportFileAsync(string sourcePath)
    {
        if (!_files.File.Exists(sourcePath))
            throw new FileNotFoundException("The selected image no longer exists.", sourcePath);
        if (_files.FileInfo.New(sourcePath).Length > MaxBytes)
            throw new InvalidDataException("The image is larger than the 25 MB limit.");
        await using var source = _files.File.OpenRead(sourcePath);
        await ImportStreamAsync(source, _files.Path.GetExtension(sourcePath));
    }

    public async Task ImportUrlAsync(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidDataException("Enter a valid HTTP or HTTPS image URL.");
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes)
            throw new InvalidDataException("The image is larger than the 25 MB limit.");
        await using var network = await response.Content.ReadAsStreamAsync();
        await using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await network.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > MaxBytes)
                throw new InvalidDataException("The image is larger than the 25 MB limit.");
            await memory.WriteAsync(buffer.AsMemory(0, read));
        }
        memory.Position = 0;
        await ImportStreamAsync(memory, _files.Path.GetExtension(uri.AbsolutePath));
    }

    public void Remove()
    {
        var old = _settings.LAUNCHER_BACKGROUND_FILE.Get();
        _settings.LAUNCHER_BACKGROUND_FILE.Set(string.Empty);
        TryDelete(old);
    }

    private async Task ImportStreamAsync(Stream source, string suggestedExtension)
    {
        await using var memory = new MemoryStream();
        await source.CopyToAsync(memory);
        memory.Position = 0;
        using var codec = SKCodec.Create(memory) ?? throw new InvalidDataException("This file is not a supported image.");
        var extension = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Gif => ".gif",
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Jpeg => ".jpg",
            SKEncodedImageFormat.Webp => ".webp",
            SKEncodedImageFormat.Bmp => ".bmp",
            SKEncodedImageFormat.Ico => ".ico",
            _ => string.IsNullOrWhiteSpace(suggestedExtension) ? ".image" : suggestedExtension.ToLowerInvariant(),
        };
        var fileName = $"background-{Guid.NewGuid():N}{extension}";
        var destination = _files.Path.Combine(_folder, fileName);
        memory.Position = 0;
        await using (var output = _files.File.Create(destination))
            await memory.CopyToAsync(output);
        var old = _settings.LAUNCHER_BACKGROUND_FILE.Get();
        _settings.LAUNCHER_BACKGROUND_FILE.Set(fileName);
        TryDelete(old);
    }

    private void TryDelete(string file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return;
        try
        {
            _files.File.Delete(_files.Path.Combine(_folder, _files.Path.GetFileName(file)));
        }
        catch { }
    }

    private void OnSettingChanged(SettingChangedSignal signal)
    {
        if (
            signal.Setting == _settings.LAUNCHER_BACKGROUND_FILE
            || signal.Setting == _settings.LAUNCHER_BACKGROUND_STRETCH
            || signal.Setting == _settings.LAUNCHER_BACKGROUND_OPACITY
            || signal.Setting == _settings.LAUNCHER_BACKGROUND_BLUR
            || signal.Setting == _settings.LAUNCHER_BACKGROUND_ZOOM
            || signal.Setting == _settings.LAUNCHER_BACKGROUND_FOCUS_X
            || signal.Setting == _settings.LAUNCHER_BACKGROUND_FOCUS_Y
        )
            Changed?.Invoke(Current);
    }

    public void Dispose() => _subscription.Dispose();
}

public sealed class LauncherBackgroundImage : Avalonia.Controls.Image, IDisposable
{
    private readonly List<Bitmap> _frames = [];
    private DispatcherTimer? _timer;
    private int[] _durations = [];
    private int _index;
    private string? _path;

    private (double Zoom, double FocusX, double FocusY) _framing = (1, 0.5, 0.5);
    private int _loadVersion;

    /// <summary>The zoom and position the picture on screen was cut with (1, 0.5, 0.5 is the whole picture).</summary>
    public (double Zoom, double FocusX, double FocusY) AppliedFraming => _framing;

    public async Task LoadAsync(LauncherBackgroundState state)
    {
        var version = ++_loadVersion;
        Stretch = state.Stretch;
        Opacity = state.Opacity;
        Effect = state.Blur > 0 ? new BlurEffect { Radius = state.Blur } : null;
        IsVisible = state.Path != null;
        if (state.Path == null)
        {
            DisposeFrames();
            return;
        }

        var framing = state.IsFramed ? (state.Zoom, state.FocusX, state.FocusY) : (1.0, 0.5, 0.5);
        var samePath = string.Equals(_path, state.Path, StringComparison.OrdinalIgnoreCase) && _frames.Count > 0;
        if (samePath && framing == _framing)
            return;

        // Dragging a slider changes the framing many times a second. Wait for it to settle, then decode once.
        if (samePath)
        {
            await Task.Delay(150);
            if (version != _loadVersion)
                return;
        }

        var decoded = await Task.Run(() => Decode(state.Path, framing.Item1, framing.Item2, framing.Item3));
        if (version != _loadVersion)
        {
            foreach (var frame in decoded.Frames)
                frame.Dispose();
            return;
        }

        // Swap only now, so changing the framing never flashes an empty background.
        DisposeFrames();
        _path = state.Path;
        _framing = framing;
        _frames.AddRange(decoded.Frames);
        _durations = decoded.Durations;
        Source = _frames[0];
        if (_frames.Count > 1)
            Schedule();
    }

    internal static (List<Bitmap> Frames, int[] Durations) Decode(string path) => Decode(path, 1, 0.5, 0.5);

    internal static (List<Bitmap> Frames, int[] Durations) Decode(string path, double zoom, double focusX, double focusY)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("The background image could not be decoded.");
        var info = codec.Info;
        var frameInfo = codec.FrameInfo;
        var count = Math.Max(1, frameInfo.Length);
        if (
            info.Width <= 0
            || info.Height <= 0
            || info.Width > 4096
            || info.Height > 4096
            || count > 300
            || (long)info.Width * info.Height * 4 * count > 256L * 1024 * 1024
        )
            throw new InvalidDataException("This image is too large to use as a background.");
        var frames = new List<Bitmap>(count);
        var durations = new int[count];
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        for (var i = 0; i < count; i++)
        {
            var required = frameInfo.Length > i ? frameInfo[i].RequiredFrame : -1;
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, new SKCodecOptions(i, required));
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                throw new InvalidDataException("A background frame could not be decoded.");
            var framed = zoom != 1 || focusX != 0.5 || focusY != 0.5 ? BackgroundFraming.Apply(bitmap, zoom, focusX, focusY) : null;
            using var image = SKImage.FromBitmap(framed ?? bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = encoded.AsStream();
            frames.Add(new Bitmap(stream));
            framed?.Dispose();
            durations[i] = frameInfo.Length > i ? Math.Max(20, frameInfo[i].Duration) : 100;
        }
        return (frames, durations);
    }

    private void Schedule()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_durations[_index]) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _index = (_index + 1) % _frames.Count;
            Source = _frames[_index];
            Schedule();
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
        _index = 0;
        _path = null;
    }

    public void Dispose() => DisposeFrames();
}
