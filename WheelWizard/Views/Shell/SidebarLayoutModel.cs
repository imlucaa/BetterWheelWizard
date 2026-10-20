using System.Text.Json;

namespace WheelWizard.Views.Shell;

public sealed class SidebarSection
{
    public string Name { get; set; } = "";
    public List<string> Items { get; set; } = [];
}

/// <summary>
/// The order of the sidebar items, the names of its categories and which items are hidden. It is saved as JSON in one
/// setting; a missing or damaged value means the default sidebar, and items that are new (or came back) always find a
/// place. Kept free of any UI so the rules can be tested.
/// </summary>
public sealed class SidebarLayoutModel
{
    public List<SidebarSection> Sections { get; set; } = [];
    public List<string> Hidden { get; set; } = [];

    /// <summary>
    /// Reads a saved layout and fixes it up: unknown or repeated items are dropped, built-in items that are missing go
    /// back to their default category, and pinned tabs that are not placed yet join the second category.
    /// </summary>
    /// <param name="builtInKeys">The always-present items, in their default order.</param>
    /// <param name="generalItemCount">How many of those belong to the first category by default.</param>
    /// <param name="pinKeys">The pinned tabs that exist right now.</param>
    public static SidebarLayoutModel Parse(
        string? json,
        IReadOnlyList<string> builtInKeys,
        int generalItemCount,
        IEnumerable<string> pinKeys
    )
    {
        SidebarLayoutModel? data = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                data = JsonSerializer.Deserialize<SidebarLayoutModel>(json);
            }
            catch (JsonException) { }
        }

        data ??= new();
        data.Sections ??= [];
        data.Hidden ??= [];
        while (data.Sections.Count < 2)
            data.Sections.Add(new());

        var pins = pinKeys.ToList();
        var valid = new HashSet<string>(builtInKeys);
        valid.UnionWith(pins);
        var seen = new HashSet<string>();
        foreach (var section in data.Sections)
        {
            section.Name ??= "";
            section.Items = (section.Items ?? []).Where(key => valid.Contains(key) && seen.Add(key)).ToList();
        }

        for (var index = 0; index < builtInKeys.Count; index++)
        {
            if (seen.Add(builtInKeys[index]))
                data.Sections[index < generalItemCount ? 0 : 1].Items.Add(builtInKeys[index]);
        }

        foreach (var key in pins)
        {
            if (seen.Add(key))
                data.Sections[1].Items.Add(key);
        }

        data.Hidden = data.Hidden.Where(valid.Contains).Distinct().ToList();
        return data;
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Moves an item to a gap in a category. <paramref name="toSlot"/> counts the gaps in the target category as they are
    /// before the item is taken out (0 is above the first item), which is what a drop line over the list shows.
    /// </summary>
    public void MoveItem(int fromSection, int fromIndex, int toSection, int toSlot)
    {
        if (!Has(fromSection) || !Has(toSection) || fromIndex < 0 || fromIndex >= Sections[fromSection].Items.Count)
            return;

        var key = Sections[fromSection].Items[fromIndex];
        Sections[fromSection].Items.RemoveAt(fromIndex);
        var position = toSlot;
        // Taking the item out shifts the places after it by one inside its own category.
        if (toSection == fromSection && fromIndex < position)
            position--;
        var target = Sections[toSection].Items;
        target.Insert(Math.Clamp(position, 0, target.Count), key);
    }

    /// <summary>
    /// Moves a whole category so that it ends up at <paramref name="toIndex"/> (counted after it is taken out). A
    /// category still on its default name keeps that name: the defaults follow the place (first is General, second is
    /// Online), so they are written down before moving.
    /// </summary>
    public void MoveSection(int from, int toIndex, Func<int, string> defaultName)
    {
        if (!Has(from))
            return;

        for (var index = 0; index < Sections.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(Sections[index].Name))
                Sections[index].Name = defaultName(index);
        }

        var moved = Sections[from];
        Sections.RemoveAt(from);
        Sections.Insert(Math.Clamp(toIndex, 0, Sections.Count), moved);
    }

    public void AddSection() => Sections.Add(new());

    /// <summary>Removes a category; its items move up to the one before it. The first category cannot be removed.</summary>
    public void RemoveSection(int index)
    {
        if (index <= 0 || !Has(index))
            return;

        Sections[index - 1].Items.AddRange(Sections[index].Items);
        Sections.RemoveAt(index);
    }

    public bool IsHidden(string key) => Hidden.Contains(key);

    public void ToggleHidden(string key)
    {
        if (!Hidden.Remove(key))
            Hidden.Add(key);
    }

    private bool Has(int section) => section >= 0 && section < Sections.Count;
}
