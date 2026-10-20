using System.IO.Abstractions;
using System.Text.Json;
using WheelWizard.ApplicationData;

namespace WheelWizard.Mods;

/// <summary>
/// Saved sets of enabled patches. A preset is a name and the titles of the patches that are on when it is applied;
/// every other patch is switched off.
/// </summary>
public interface IModPresetStore
{
    /// <summary>The saved presets by name, oldest name first. A missing or unreadable file gives an empty list.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Load();

    /// <summary>Saves the preset, replacing one with the same name (names are compared ignoring case).</summary>
    OperationResult Save(string name, IEnumerable<string> enabledTitles);

    OperationResult Delete(string name);
}

public sealed class ModPresetStore(IApplicationDataLocation applicationData, IFileSystem fileSystem) : IModPresetStore
{
    private string FilePath => fileSystem.Path.Combine(applicationData.DirectoryPath, "ModPresets.json");

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Load()
    {
        try
        {
            if (!fileSystem.File.Exists(FilePath))
                return new Dictionary<string, IReadOnlyList<string>>();

            var stored = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(fileSystem.File.ReadAllText(FilePath));
            return (stored ?? [])
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)(entry.Value ?? []), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }
    }

    public OperationResult Save(string name, IEnumerable<string> enabledTitles)
    {
        name = name.Trim();
        if (name.Length == 0)
            return Fail("Give the preset a name.");

        var presets = Load().ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        presets[name] = enabledTitles.ToList();
        return Write(presets);
    }

    public OperationResult Delete(string name)
    {
        var presets = Load().ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        return presets.Remove(name) ? Write(presets) : Ok();
    }

    private OperationResult Write(Dictionary<string, IReadOnlyList<string>> presets)
    {
        try
        {
            fileSystem.Directory.CreateDirectory(applicationData.DirectoryPath);
            // Write next to the file, then move it over, so a crash never leaves half a file behind.
            var temp = FilePath + ".tmp";
            fileSystem.File.WriteAllText(temp, JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true }));
            fileSystem.File.Move(temp, FilePath, overwrite: true);
            return Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail("Could not save the preset.");
        }
    }
}
