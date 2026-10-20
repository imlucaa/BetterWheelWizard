using System.Text.Json;
using WheelWizard.Models.Enums;

namespace WheelWizard.GameDiscs;

/// <summary>
/// Your Mario Kart Wii discs, one per region. The launcher still starts one disc at a time (the game location), so choosing
/// a region switches the game location to that region's disc.
/// </summary>
public static class RegionDiscs
{
    public static string? PathFor(string json, MarioKartWiiEnums.Regions region)
    {
        var all = Parse(json);
        return all.TryGetValue(region.ToString(), out var path) && !string.IsNullOrWhiteSpace(path) ? path : null;
    }

    /// <summary>The stored list with this region's disc set, or removed when <paramref name="path"/> is null.</summary>
    public static string WithPath(string json, MarioKartWiiEnums.Regions region, string? path)
    {
        var all = Parse(json);
        if (string.IsNullOrWhiteSpace(path))
            all.Remove(region.ToString());
        else
            all[region.ToString()] = path;
        return JsonSerializer.Serialize(all);
    }

    private static Dictionary<string, string> Parse(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Reads the game id from the start of a disc image (plain ISO, WBFS, RVZ or WIA) and maps it to a region.</summary>
    public static MarioKartWiiEnums.Regions? DetectRegion(ReadOnlySpan<byte> head)
    {
        var offset = 0;
        if (head.Length >= 4 && head[..4].SequenceEqual("WBFS"u8))
            offset = 0x200;
        else if (head.Length >= 4 && (head[..4].SequenceEqual("RVZ\x01"u8) || head[..4].SequenceEqual("WIA\x01"u8)))
            offset = 0x58;

        if (head.Length < offset + 6)
            return null;

        var id = System.Text.Encoding.ASCII.GetString(head.Slice(offset, 6));
        if (!id.StartsWith("RMC", StringComparison.Ordinal))
            return null;

        return id[3] switch
        {
            'E' => MarioKartWiiEnums.Regions.America,
            'P' => MarioKartWiiEnums.Regions.Europe,
            'J' => MarioKartWiiEnums.Regions.Japan,
            'K' => MarioKartWiiEnums.Regions.Korea,
            _ => null,
        };
    }

    public static MarioKartWiiEnums.Regions? DetectRegionFromFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var head = new byte[0x300];
            var read = stream.Read(head, 0, head.Length);
            return DetectRegion(head.AsSpan(0, read));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
