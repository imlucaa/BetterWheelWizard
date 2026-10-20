using System.Reflection;
using WheelWizard.ApplicationData;

namespace WheelWizard.StreamOverlay;

/// <summary>The overlay page that ships inside Wheel Wizard (MIT licensed, see Resources/Overlay/LICENSE.txt).</summary>
public static class OverlayAssets
{
    public static (byte[] Bytes, string ContentType)? Get(string name)
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Overlay/{name}");
        if (stream is null)
            return null;

        using (stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var type = Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".js" => "text/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".html" => "text/html; charset=utf-8",
                _ => "application/octet-stream",
            };
            return (memory.ToArray(), type);
        }
    }
}

/// <summary>
/// The picture (or GIF) chosen for the overlay background. It is copied next to the launcher's data and served from the
/// same local address as the overlay page, which is the only place the page is allowed to load pictures from.
/// </summary>
public sealed class OverlayBackground(IApplicationDataLocation location)
{
    private const long MaxBytes = 25L * 1024 * 1024;
    private static readonly string[] Allowed = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];

    private string Folder => Path.Combine(location.DirectoryPath, "overlay");

    public string? CurrentPath
    {
        get
        {
            try
            {
                return Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, "background.*").FirstOrDefault() : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>The link of the stored picture. The number changes with every new picture so OBS never shows an old one.</summary>
    public string Url(long version) => $"http://127.0.0.1:{DiscordPresence.RoomLinkListener.Port}/overlay/bg?v={version}";

    public static bool IsLocalUrl(string url) =>
        url.StartsWith($"http://127.0.0.1:{DiscordPresence.RoomLinkListener.Port}/overlay/bg", StringComparison.Ordinal);

    /// <summary>Copies the picked file in. Returns an error text, or null when it worked.</summary>
    public string? Import(string sourcePath)
    {
        try
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!Allowed.Contains(extension))
                return "Pick a PNG, JPG, GIF, WEBP or BMP picture.";
            if (new FileInfo(sourcePath).Length > MaxBytes)
                return "That picture is bigger than 25 MB.";

            Directory.CreateDirectory(Folder);
            // Copy first so a failed copy keeps the picture that was already set.
            var staging = Path.Combine(Folder, $"incoming{extension}");
            File.Copy(sourcePath, staging, overwrite: true);
            Clear();
            File.Move(staging, Path.Combine(Folder, $"background{extension}"), overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "Could not copy that picture.";
        }
    }

    public void Clear()
    {
        try
        {
            if (Directory.Exists(Folder))
                foreach (var file in Directory.EnumerateFiles(Folder, "background.*"))
                    File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public (byte[] Bytes, string ContentType)? Read()
    {
        var path = CurrentPath;
        if (path is null)
            return null;

        try
        {
            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream",
            };
            return (File.ReadAllBytes(path), type);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
