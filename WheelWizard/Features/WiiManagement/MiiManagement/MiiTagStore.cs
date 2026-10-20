using System.Text.Json;
using WheelWizard.Settings;
using WheelWizard.Settings.Views;

namespace WheelWizard.WiiManagement.MiiManagement;

/// <summary>
/// The player's own Mii tags: a name plus the Miis in it. Kept in the launcher settings, so nothing in the Mii database changes.
/// </summary>
public sealed class MiiTagStore(ISettingsManager settings)
{
    private Dictionary<string, List<uint>> Load()
    {
        try
        {
            var json = settings.Get<string>(settings.MII_TAGS);
            if (string.IsNullOrWhiteSpace(json))
                return new(StringComparer.OrdinalIgnoreCase);

            var data = JsonSerializer.Deserialize<Dictionary<string, List<uint>>>(json);
            return data is null ? new(StringComparer.OrdinalIgnoreCase) : new(data, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save(Dictionary<string, List<uint>> data) =>
        SettingsEditing.Set(settings, settings.MII_TAGS, JsonSerializer.Serialize(data));

    public IReadOnlyList<string> Tags => Load().Keys.ToList();

    public bool Contains(string tag, uint miiId) => Load().TryGetValue(tag, out var ids) && ids.Contains(miiId);

    public bool Create(string tag)
    {
        tag = tag.Trim();
        if (tag.Length == 0)
            return false;

        var data = Load();
        if (data.ContainsKey(tag))
            return false;

        data[tag] = [];
        Save(data);
        return true;
    }

    public void Rename(string tag, string newName)
    {
        newName = newName.Trim();
        var data = Load();
        if (newName.Length == 0 || !data.Remove(tag, out var ids) || data.ContainsKey(newName))
            return;

        data[newName] = ids;
        Save(data);
    }

    public void Delete(string tag)
    {
        var data = Load();
        if (data.Remove(tag))
            Save(data);
    }

    public void SetMembership(string tag, IEnumerable<uint> miiIds, bool member)
    {
        var data = Load();
        if (!data.TryGetValue(tag, out var ids))
            return;

        foreach (var id in miiIds)
        {
            if (member && !ids.Contains(id))
                ids.Add(id);
            else if (!member)
                ids.Remove(id);
        }

        Save(data);
    }
}
