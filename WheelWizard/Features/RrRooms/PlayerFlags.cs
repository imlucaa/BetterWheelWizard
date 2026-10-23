using System.Text.Json;
using WheelWizard.Models.RRInfo;
using WheelWizard.Settings;
using WheelWizard.Settings.Views;

namespace WheelWizard.RrRooms;

/// <summary>A player you flagged: your note ("Wii off") and the colour the tag is shown in ("#RRGGBB").</summary>
public sealed record PlayerFlag(string Note, string Color);

/// <summary>
/// Players you flagged yourself, with a short note ("Wii off", "VR farmer") and a colour, so they stand out in a room and
/// rooms with them in can be avoided. Kept in the launcher settings by friend code; nothing is sent anywhere.
/// </summary>
public static class PlayerFlags
{
    public const int MaxNoteLength = 24;

    /// <summary>Ready-made notes offered with one click.</summary>
    public static readonly IReadOnlyList<string> QuickNotes = ["Wii off", "VR farmer", "Troll", "Cheater"];

    /// <summary>The tag colours to pick from. All are light enough for dark text on top and to stand out on a dark page.</summary>
    public static readonly IReadOnlyList<(string Name, string Hex)> Colors =
    [
        ("Red", "#FF6581"),
        ("Orange", "#FB923C"),
        ("Yellow", "#FACC15"),
        ("Green", "#4ADE80"),
        ("Blue", "#60A5FA"),
        ("Purple", "#C084FC"),
    ];

    /// <summary>A new flag starts red, the colour flags always had.</summary>
    public static string DefaultColor => Colors[0].Hex;

    /// <summary>The ready-made notes, then any other note already used, so a custom note is one click the next time.</summary>
    public static IReadOnlyList<string> NoteChoices(IReadOnlyDictionary<string, PlayerFlag> flags) =>
        QuickNotes
            .Concat(flags.Values.Select(flag => flag.Note).Where(note => !QuickNotes.Contains(note, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The saved flags by friend code (digits only). A missing or damaged value gives no flags. Flags saved before
    /// colours existed (just a note) come back red.
    /// </summary>
    public static Dictionary<string, PlayerFlag> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
            var flags = new Dictionary<string, PlayerFlag>();
            foreach (var (code, value) in stored)
            {
                var digits = Digits(code);
                if (digits.Length != 12)
                    continue;

                string? note = null;
                string? color = null;
                if (value.ValueKind == JsonValueKind.String)
                    note = value.GetString();
                else if (value.ValueKind == JsonValueKind.Object)
                {
                    if (value.TryGetProperty("Note", out var noteValue) && noteValue.ValueKind == JsonValueKind.String)
                        note = noteValue.GetString();
                    if (value.TryGetProperty("Color", out var colorValue) && colorValue.ValueKind == JsonValueKind.String)
                        color = colorValue.GetString();
                }

                if (!string.IsNullOrWhiteSpace(note))
                    flags[digits] = new PlayerFlag(Clean(note), KnownColor(color));
            }

            return flags;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The saved flags with this player flagged with <paramref name="note"/>, or unflagged when it is empty. Without a
    /// <paramref name="color"/> an existing flag keeps its colour and a new one is red.
    /// </summary>
    public static string With(string? json, string friendCode, string? note, string? color = null)
    {
        var flags = Parse(json);
        var digits = Digits(friendCode);
        if (digits.Length == 12)
        {
            if (string.IsNullOrWhiteSpace(note))
                flags.Remove(digits);
            else
            {
                var keep = flags.TryGetValue(digits, out var existing) ? existing.Color : DefaultColor;
                flags[digits] = new PlayerFlag(Clean(note), color is null ? keep : KnownColor(color));
            }
        }

        return JsonSerializer.Serialize(flags);
    }

    /// <summary>The saved flags with a flagged player's tag in another colour. A player who is not flagged is left alone.</summary>
    public static string WithColor(string? json, string friendCode, string color)
    {
        var flags = Parse(json);
        if (flags.TryGetValue(Digits(friendCode), out var existing))
            flags[Digits(friendCode)] = existing with { Color = KnownColor(color) };
        return JsonSerializer.Serialize(flags);
    }

    /// <summary>The flag for a player, or null when they are not flagged.</summary>
    public static PlayerFlag? FlagFor(IReadOnlyDictionary<string, PlayerFlag> flags, string? friendCode) =>
        flags.TryGetValue(Digits(friendCode), out var flag) ? flag : null;

    /// <summary>The note for a player, or an empty text when they are not flagged.</summary>
    public static string NoteFor(IReadOnlyDictionary<string, PlayerFlag> flags, string? friendCode) =>
        FlagFor(flags, friendCode)?.Note ?? string.Empty;

    /// <summary>
    /// Puts the notes and colours on the players and a short summary on each room: the note when one flagged player is
    /// in it, otherwise how many are. The room's tag takes the colour of its first flagged player.
    /// </summary>
    public static void Apply(IEnumerable<RrRoom> rooms, IReadOnlyDictionary<string, PlayerFlag> flags)
    {
        foreach (var room in rooms)
        {
            var found = new List<PlayerFlag>();
            foreach (var player in room.Players)
            {
                var flag = FlagFor(flags, player.FriendCode);
                player.FlagNote = flag?.Note ?? string.Empty;
                player.FlagColor = flag?.Color ?? DefaultColor;
                if (flag is not null)
                    found.Add(flag);
            }

            room.FlagColor = found.Count > 0 ? found[0].Color : DefaultColor;
            room.FlagSummary =
                found.Count == 0 ? string.Empty
                : found.Count == 1 ? found[0].Note
                : $"{found.Count} flagged";
        }
    }

    public static IReadOnlyDictionary<string, PlayerFlag> Load(ISettingsManager settings) =>
        Parse(settings.Get<string>(settings.PLAYER_FLAGS));

    public static void Save(ISettingsManager settings, string friendCode, string? note) =>
        SettingsEditing.Set(settings, settings.PLAYER_FLAGS, With(settings.Get<string>(settings.PLAYER_FLAGS), friendCode, note));

    public static void SaveColor(ISettingsManager settings, string friendCode, string color) =>
        SettingsEditing.Set(settings, settings.PLAYER_FLAGS, WithColor(settings.Get<string>(settings.PLAYER_FLAGS), friendCode, color));

    // Only the colours on offer are kept, so a damaged value can never give an unreadable tag.
    private static string KnownColor(string? color) =>
        Colors.FirstOrDefault(entry => string.Equals(entry.Hex, color, StringComparison.OrdinalIgnoreCase)).Hex ?? DefaultColor;

    private static string Clean(string note)
    {
        var trimmed = note.Trim();
        return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength].TrimEnd();
    }

    private static string Digits(string? friendCode) => new((friendCode ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
