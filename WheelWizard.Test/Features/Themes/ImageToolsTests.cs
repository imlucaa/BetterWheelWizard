using Avalonia.Media;
using SkiaSharp;
using WheelWizard.Themes;

namespace WheelWizard.Test.Features.Themes;

public class ImageToolsTests
{
    [Fact]
    public void CropRect_OfAWideImage_KeepsFullHeightAndSlidesAcrossTheWidth()
    {
        Assert.Equal((625, 0, 750, 1000), BackgroundFraming.CropRect(2000, 1000, 0.75, 1, 0.5, 0.5));
        Assert.Equal((0, 0, 750, 1000), BackgroundFraming.CropRect(2000, 1000, 0.75, 1, 0, 0.5));
        Assert.Equal((1250, 0, 750, 1000), BackgroundFraming.CropRect(2000, 1000, 0.75, 1, 1, 0.5));
    }

    [Fact]
    public void CropRect_OfATallImage_KeepsFullWidthAndSlidesDown()
    {
        var (_, top, width, height) = BackgroundFraming.CropRect(1000, 2000, 0.75, 1, 0.5, 0);
        var (_, bottom, _, _) = BackgroundFraming.CropRect(1000, 2000, 0.75, 1, 0.5, 1);

        Assert.Equal(1000, width);
        Assert.Equal(1333, height);
        Assert.Equal(0, top);
        Assert.Equal(2000 - 1333, bottom);
    }

    [Fact]
    public void CropRect_ZoomShowsLess_OfTheSameMiddle()
    {
        var (x, y, width, height) = BackgroundFraming.CropRect(2000, 1000, 0.75, 2, 0.5, 0.5);

        Assert.Equal((375, 500), (width, height));
        Assert.Equal(812, x);
        Assert.Equal(250, y);
    }

    [Fact]
    public void CropRect_ClampsNonsenseInput()
    {
        Assert.Equal(BackgroundFraming.CropRect(800, 600, 0.75, 3, 0.5, 0.5), BackgroundFraming.CropRect(800, 600, 0.75, 99, 0.5, 0.5));
        Assert.Equal(
            BackgroundFraming.CropRect(800, 600, 0.75, 1, 0.5, 0.5),
            BackgroundFraming.CropRect(800, 600, 0.75, double.NaN, double.NaN, double.NaN)
        );
        Assert.Equal(BackgroundFraming.CropRect(800, 600, 0.75, 1, 0, 1), BackgroundFraming.CropRect(800, 600, 0.75, 0.2, -5, 7));
        Assert.Equal((0, 0, 0, 0), BackgroundFraming.CropRect(0, 0, 0.75, 1, 0.5, 0.5));
    }

    [Fact]
    public void CropRect_NeverLeavesTheImage()
    {
        var random = new Random(99);
        for (var i = 0; i < 2000; i++)
        {
            var imageWidth = random.Next(1, 5000);
            var imageHeight = random.Next(1, 5000);
            var (x, y, width, height) = BackgroundFraming.CropRect(
                imageWidth,
                imageHeight,
                BackgroundFraming.ViewAspect,
                1 + random.NextDouble() * 2,
                random.NextDouble(),
                random.NextDouble()
            );

            Assert.True(x >= 0 && y >= 0 && width >= 1 && height >= 1, $"{imageWidth}x{imageHeight}: {x},{y} {width}x{height}");
            Assert.True(x + width <= imageWidth && y + height <= imageHeight, $"{imageWidth}x{imageHeight}: {x},{y} {width}x{height}");
        }
    }

    [Fact]
    public void Apply_ShowsTheChosenSide_OfAWideImage()
    {
        using var source = new SKBitmap(2000, 1000);
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(SKColors.Red);
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(1000, 0, 1000, 1000, blue);
        }

        using var left = BackgroundFraming.Apply(source, 1, 0, 0.5);
        using var right = BackgroundFraming.Apply(source, 1, 1, 0.5);

        Assert.Equal(SKColors.Red, left.GetPixel(left.Width / 2, left.Height / 2));
        Assert.Equal(SKColors.Blue, right.GetPixel(right.Width / 2, right.Height / 2));
        Assert.Equal(left.Width, right.Width);
    }

    [Fact]
    public void Apply_ShrinksBigCrops_ToTheLimit()
    {
        using var source = new SKBitmap(4000, 3000);

        using var result = BackgroundFraming.Apply(source, 1, 0.5, 0.5, maxSide: 600);

        Assert.True(Math.Max(result.Width, result.Height) <= 600);
        Assert.Equal(BackgroundFraming.ViewAspect, (double)result.Width / result.Height, 2);
    }

    [Fact]
    public void State_IsOnlyFramedWhenFillingTheWindow_AndMovedOffTheDefault()
    {
        Assert.False(new LauncherBackgroundState("a", Stretch.UniformToFill, 1).IsFramed);
        Assert.True(new LauncherBackgroundState("a", Stretch.UniformToFill, 1, Zoom: 1.5).IsFramed);
        Assert.True(new LauncherBackgroundState("a", Stretch.UniformToFill, 1, FocusX: 0.2).IsFramed);
        Assert.False(new LauncherBackgroundState("a", Stretch.Uniform, 1, Zoom: 2).IsFramed);
    }

    private static (byte, byte, byte) Px(byte r, byte g, byte b) => (r, g, b);

    private static (int Hue, double Saturation) HueOf(string hex)
    {
        var r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0;
        var g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0;
        var b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        if (max == min)
            return (0, 0);
        var delta = max - min;
        var hue =
            max == r ? 60 * (((g - b) / delta % 6 + 6) % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        return ((int)Math.Round(hue) % 360, delta / max);
    }

    [Fact]
    public void Palette_OfARedImage_IsRed_WithLightTextAndDarkBackground()
    {
        var colors = ImagePalette.FromPixels(Enumerable.Repeat(Px(230, 40, 40), 200).ToList());

        var (hue, saturation) = HueOf(colors.Accent);
        Assert.True(hue is < 15 or > 345, $"hue {hue}");
        Assert.True(saturation >= 0.5);
        Assert.True(HueOf(colors.Background).Saturation > 0);
        Assert.StartsWith("#F", colors.Text);
        Assert.Equal("#17", colors.Background[..3]);
    }

    [Fact]
    public void Palette_UsesASecondStrongHue_ForTheTitle()
    {
        var pixels = Enumerable.Repeat(Px(230, 40, 40), 300).Concat(Enumerable.Repeat(Px(40, 60, 230), 200)).ToList();

        var colors = ImagePalette.FromPixels(pixels);

        var (titleHue, _) = HueOf(colors.Branding);
        Assert.InRange(titleHue, 215, 245);
        var (accentHue, _) = HueOf(colors.Accent);
        Assert.True(accentHue is < 15 or > 345);
    }

    [Fact]
    public void Palette_WithOnlyAFaintSecondHue_ShiftsTheTitleInsteadOfCopyingNoise()
    {
        var pixels = Enumerable.Repeat(Px(230, 40, 40), 1000).Concat(Enumerable.Repeat(Px(40, 60, 230), 5)).ToList();

        var colors = ImagePalette.FromPixels(pixels);

        var (titleHue, _) = HueOf(colors.Branding);
        Assert.InRange(titleHue, 20, 50);
    }

    [Fact]
    public void Palette_OfGrayOrEmptyImages_IsNeutral()
    {
        var gray = ImagePalette.FromPixels(Enumerable.Repeat(Px(120, 120, 120), 100).ToList());
        var empty = ImagePalette.FromPixels([]);

        Assert.Equal(gray, empty);
        Assert.Equal("#E5E7EB", gray.Accent);
    }

    [Fact]
    public void Palette_IgnoresTooDarkAndTooPalePixels()
    {
        var pixels = Enumerable
            .Repeat(Px(10, 0, 0), 500)
            .Concat(Enumerable.Repeat(Px(250, 245, 245), 500))
            .Concat(Enumerable.Repeat(Px(40, 200, 90), 20))
            .ToList();

        var colors = ImagePalette.FromPixels(pixels);

        var (hue, _) = HueOf(colors.Accent);
        Assert.InRange(hue, 125, 150);
    }

    [Fact]
    public void FromFile_ReadsARealImage_AndRejectsMissingOrBrokenFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "bww-palette-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var good = Path.Combine(folder, "green.png");
            using (var bitmap = new SKBitmap(80, 60))
            {
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(new SKColor(30, 200, 80));
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(good, data.ToArray());
            }

            var broken = Path.Combine(folder, "broken.png");
            File.WriteAllText(broken, "this is not an image");

            var colors = ImagePalette.FromFile(good);

            Assert.NotNull(colors);
            Assert.InRange(HueOf(colors.Value.Accent).Hue, 125, 150);
            Assert.Null(ImagePalette.FromFile(broken));
            Assert.Null(ImagePalette.FromFile(Path.Combine(folder, "missing.png")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
