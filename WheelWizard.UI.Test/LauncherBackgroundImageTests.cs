using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using WheelWizard.Themes;

namespace WheelWizard.UI.Test;

public class LauncherBackgroundImageTests
{
    private static string WriteImage(string folder, int width, int height)
    {
        var path = Path.Combine(folder, "wide.png");
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (string Folder, string Path) Create()
    {
        var folder = Path.Combine(Path.GetTempPath(), "bww-bg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return (folder, WriteImage(folder, 400, 200));
    }

    [AvaloniaFact]
    public async Task Framing_IsAppliedWhileFilling_AndResetForTheDefault()
    {
        var (folder, path) = Create();
        using var image = new LauncherBackgroundImage();
        try
        {
            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1));
            Assert.NotNull(image.Source);
            Assert.Equal((1.0, 0.5, 0.5), image.AppliedFraming);

            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1, Zoom: 1.5, FocusX: 1, FocusY: 0.25));
            Assert.NotNull(image.Source);
            Assert.Equal((1.5, 1.0, 0.25), image.AppliedFraming);

            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1));
            Assert.Equal((1.0, 0.5, 0.5), image.AppliedFraming);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Framing_IsIgnored_WhenTheImageIsNotFillingTheWindow()
    {
        var (folder, path) = Create();
        using var image = new LauncherBackgroundImage();
        try
        {
            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.Uniform, 1, Zoom: 2, FocusX: 0));

            Assert.NotNull(image.Source);
            Assert.Equal((1.0, 0.5, 0.5), image.AppliedFraming);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Blur_IsAnEffectOnlyWhileItIsAboveZero()
    {
        var (folder, path) = Create();
        using var image = new LauncherBackgroundImage();
        try
        {
            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 0.5, Blur: 12));
            var blur = Assert.IsType<BlurEffect>(image.Effect);
            Assert.Equal(12, blur.Radius);

            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 0.5));
            Assert.Null(image.Effect);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task SlidingTheFramingQuickly_EndsOnTheLastValue()
    {
        var (folder, path) = Create();
        using var image = new LauncherBackgroundImage();
        try
        {
            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1));
            var first = image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1, Zoom: 1.2));
            var second = image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1, Zoom: 2.5));
            await Task.WhenAll(first, second);

            Assert.NotNull(image.Source);
            Assert.Equal((2.5, 0.5, 0.5), image.AppliedFraming);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task RemovingTheImage_HidesTheControl()
    {
        var (folder, path) = Create();
        using var image = new LauncherBackgroundImage();
        try
        {
            await image.LoadAsync(new LauncherBackgroundState(path, Stretch.UniformToFill, 1));
            Assert.True(image.IsVisible);

            await image.LoadAsync(new LauncherBackgroundState(null, Stretch.UniformToFill, 1));

            Assert.False(image.IsVisible);
            Assert.Null(image.Source);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
