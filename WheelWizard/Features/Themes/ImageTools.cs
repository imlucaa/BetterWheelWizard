using SkiaSharp;

namespace WheelWizard.Themes;

/// <summary>Picks the part of a background image that is shown: how far it is zoomed in and which part stays in view.</summary>
public static class BackgroundFraming
{
    /// <summary>The launcher window is a fixed 656 x 876, so the image is framed for that shape.</summary>
    public const double ViewAspect = 656.0 / 876.0;

    public const double MaxZoom = 3.0;

    /// <summary>
    /// The part of an image (in pixels) that fills a view with the given aspect ratio. Zoom 1 shows as much of the image as
    /// fits; a larger zoom shows less. <paramref name="focusX"/> and <paramref name="focusY"/> (0 to 1) slide the visible
    /// part across the image: 0.5 is the middle, 0 the left or top edge, 1 the right or bottom edge.
    /// </summary>
    public static (int X, int Y, int Width, int Height) CropRect(
        int imageWidth,
        int imageHeight,
        double viewAspect,
        double zoom,
        double focusX,
        double focusY
    )
    {
        if (imageWidth <= 0 || imageHeight <= 0 || viewAspect <= 0)
            return (0, 0, Math.Max(imageWidth, 0), Math.Max(imageHeight, 0));

        zoom = Math.Clamp(double.IsNaN(zoom) ? 1 : zoom, 1, MaxZoom);
        focusX = Math.Clamp(double.IsNaN(focusX) ? 0.5 : focusX, 0, 1);
        focusY = Math.Clamp(double.IsNaN(focusY) ? 0.5 : focusY, 0, 1);

        double width;
        double height;
        if ((double)imageWidth / imageHeight > viewAspect)
        {
            height = imageHeight;
            width = height * viewAspect;
        }
        else
        {
            width = imageWidth;
            height = width / viewAspect;
        }

        width = Math.Max(1, width / zoom);
        height = Math.Max(1, height / zoom);
        var x = (int)Math.Round((imageWidth - width) * focusX);
        var y = (int)Math.Round((imageHeight - height) * focusY);
        var w = Math.Min(imageWidth - x, Math.Max(1, (int)Math.Round(width)));
        var h = Math.Min(imageHeight - y, Math.Max(1, (int)Math.Round(height)));
        return (x, y, w, h);
    }

    /// <summary>Crops the bitmap to the framing and shrinks it so the longest side is at most <paramref name="maxSide"/>.</summary>
    public static SKBitmap Apply(SKBitmap source, double zoom, double focusX, double focusY, int maxSide = 2400)
    {
        var (x, y, width, height) = CropRect(source.Width, source.Height, ViewAspect, zoom, focusX, focusY);
        var cropped = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        if (!source.ExtractSubset(cropped, new SKRectI(x, y, x + width, y + height)))
        {
            cropped.Dispose();
            throw new InvalidDataException("The background image could not be cropped.");
        }

        var longest = Math.Max(width, height);
        if (longest <= maxSide)
            return cropped;

        var scale = (double)maxSide / longest;
        var resized = cropped.Resize(
            new SKImageInfo(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)), source.ColorType, source.AlphaType),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
        );
        cropped.Dispose();
        return resized ?? throw new InvalidDataException("The background image could not be resized.");
    }
}

/// <summary>Chooses four matching launcher colors from the colors that are most common in an image.</summary>
public static class ImagePalette
{
    private const int Buckets = 12;
    private const double BucketSize = 360.0 / Buckets;
    private const int SampleSize = 64;

    /// <summary>The theme colors for an image file, or null when the file is not an image that can be read.</summary>
    public static RandomTheme.Colors? FromFile(string path)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec is null)
                return null;

            using var full = SKBitmap.Decode(codec);
            if (full is null || full.Width <= 0 || full.Height <= 0)
                return null;

            var width = Math.Min(SampleSize, full.Width);
            var height = Math.Min(SampleSize, full.Height);
            using var small = full.Resize(
                new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            );
            if (small is null)
                return null;

            var pixels = new List<(byte R, byte G, byte B)>(width * height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = small.GetPixel(x, y);
                    if (pixel.Alpha >= 128)
                        pixels.Add((pixel.Red, pixel.Green, pixel.Blue));
                }
            }

            return FromPixels(pixels);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Counts the colorful pixels by hue, weighting vivid, bright ones more. The strongest hue becomes the main color and
    /// the strongest hue that is clearly different becomes the title color. A grayscale image gets neutral colors.
    /// </summary>
    public static RandomTheme.Colors FromPixels(IReadOnlyList<(byte R, byte G, byte B)> pixels)
    {
        var weight = new double[Buckets];
        var hueSum = new double[Buckets];
        var saturationSum = new double[Buckets];
        var valueSum = new double[Buckets];
        var count = new int[Buckets];

        foreach (var (r, g, b) in pixels)
        {
            var (hue, saturation, value) = ToHsv(r, g, b);
            if (saturation < 0.25 || value < 0.2)
                continue;

            var bucket = (int)(hue / BucketSize) % Buckets;
            var w = saturation * value;
            weight[bucket] += w;
            hueSum[bucket] += hue * w;
            saturationSum[bucket] += saturation * w;
            valueSum[bucket] += value * w;
            count[bucket]++;
        }

        var best = IndexOfMax(weight, -1);
        if (best < 0 || weight[best] <= 0)
            return new RandomTheme.Colors("#E5E7EB", "#9CA3AF", "#F5F5F5", "#0B0B0C");

        double HueOf(int bucket) => hueSum[bucket] / weight[bucket];

        var mainHue = HueOf(best);
        var mainSaturation = Math.Clamp(saturationSum[best] / weight[best], 0.55, 0.9);
        var mainValue = Math.Clamp(valueSum[best] / weight[best] + 0.1, 0.85, 1.0);

        // The title color: the next strongest hue that is not next to the main one, if it matters at all.
        double titleHue;
        var second = -1;
        var secondWeight = 0.0;
        for (var i = 0; i < Buckets; i++)
        {
            var distance = Math.Min(Math.Abs(i - best), Buckets - Math.Abs(i - best));
            if (distance >= 2 && weight[i] > secondWeight)
            {
                second = i;
                secondWeight = weight[i];
            }
        }

        if (second >= 0 && secondWeight >= weight[best] * 0.25)
            titleHue = HueOf(second);
        else
            titleHue = (mainHue + 35) % 360;

        return new RandomTheme.Colors(
            RandomTheme.Hex(mainHue, mainSaturation, mainValue),
            RandomTheme.Hex(titleHue, Math.Clamp(mainSaturation - 0.15, 0.35, 0.75), 1.0),
            RandomTheme.Hex(mainHue, 0.06, 0.96),
            RandomTheme.Hex(mainHue, Math.Clamp(mainSaturation - 0.2, 0.3, 0.55), 0.09)
        );
    }

    private static int IndexOfMax(double[] values, int exclude)
    {
        var index = -1;
        for (var i = 0; i < values.Length; i++)
        {
            if (i != exclude && (index < 0 || values[i] > values[index]))
                index = i;
        }

        return index;
    }

    private static (double Hue, double Saturation, double Value) ToHsv(byte r, byte g, byte b)
    {
        double red = r / 255.0,
            green = g / 255.0,
            blue = b / 255.0;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        if (delta == 0)
            return (0, 0, max);

        double hue;
        if (max == red)
            hue = 60 * (((green - blue) / delta % 6 + 6) % 6);
        else if (max == green)
            hue = 60 * ((blue - red) / delta + 2);
        else
            hue = 60 * ((red - green) / delta + 4);
        return (hue % 360, delta / max, max);
    }
}
