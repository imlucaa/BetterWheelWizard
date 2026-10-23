namespace WheelWizard.Ghosts;

/// <summary>A track on RWFC's time trial board (rwfc.net/tt). The names are the same as in Retro Rewind's ghost lists.</summary>
public sealed class RwfcTimeTrialTrack
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Laps { get; set; }
    public bool SupportsGlitch { get; set; }
    public bool IsHidden { get; set; }
}

/// <summary>One page of a track's time trial board.</summary>
public sealed class RwfcTimeTrialBoard
{
    public RwfcTimeTrialTrack? Track { get; set; }
    public int Cc { get; set; }
    public List<RwfcTimeTrialTime> Submissions { get; set; } = [];
    public int TotalSubmissions { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>One time on the board, with the run's ghost when <see cref="HasGhostFile"/> is set.</summary>
public sealed class RwfcTimeTrialTime
{
    public int Id { get; set; }
    public int TrackId { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public int TtProfileId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? CountryAlpha2 { get; set; }
    public int Cc { get; set; }
    public int FinishTimeMs { get; set; }
    public string FinishTimeDisplay { get; set; } = string.Empty;
    public string? MiiName { get; set; }
    public int VehicleId { get; set; }
    public int CharacterId { get; set; }
    public int ControllerType { get; set; }
    public int DriftType { get; set; }
    public int DriftCategory { get; set; }
    public bool Shroomless { get; set; }
    public bool Glitch { get; set; }
    public bool IsFlap { get; set; }
    public string? FastestLapDisplay { get; set; }
    public string? DateSet { get; set; }

    /// <summary>The place on the board; null for a player's own list of times.</summary>
    public int? Rank { get; set; }
    public bool HasGhostFile { get; set; }
    public List<string> LapSplitsDisplay { get; set; } = [];
}

/// <summary>A track and its current world record (null when nobody has set one) for the chosen filters.</summary>
public sealed class RwfcTrackRecord
{
    public int TrackId { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public RwfcTimeTrialTime? ActiveWorldRecord { get; set; }
}

/// <summary>One page of the player rankings: players by the number of world records they hold.</summary>
public sealed class RwfcTimeTrialRankings
{
    public List<RwfcRankedPlayer> Players { get; set; } = [];
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalPlayers { get; set; }
}

public sealed class RwfcRankedPlayer
{
    public int Rank { get; set; }
    public int TtProfileId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? CountryAlpha2 { get; set; }
    public int WorldRecordCount { get; set; }
}

/// <summary>A player's time trial profile: name, country and how many records and times they have.</summary>
public sealed class RwfcTimeTrialProfile
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int TotalSubmissions { get; set; }
    public int CurrentWorldRecords { get; set; }
    public string? CountryAlpha2 { get; set; }
}

/// <summary>One page of a player's times.</summary>
public sealed class RwfcProfileSubmissions
{
    public List<RwfcTimeTrialTime> Submissions { get; set; } = [];
    public int TotalSubmissions { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>The filters the time trial board offers, the same ones as on rwfc.net/tt.</summary>
public sealed record TimeTrialFilter(
    int Cc = 150,
    bool FastestLap = false,
    TimeTrialVehicles Vehicles = TimeTrialVehicles.All,
    TimeTrialShrooms Shrooms = TimeTrialShrooms.Any,
    bool GlitchAllowed = true
)
{
    /// <summary>The value of the board's <c>vehicle</c> filter, or null for all vehicles.</summary>
    public string? VehicleQuery =>
        Vehicles switch
        {
            TimeTrialVehicles.Karts => "karts",
            TimeTrialVehicles.Bikes => "bikes",
            _ => null,
        };

    /// <summary>The value of the board's <c>shroomless</c> filter, or null for any run.</summary>
    public string? ShroomlessQuery =>
        Shrooms switch
        {
            TimeTrialShrooms.ShroomlessOnly => "only",
            TimeTrialShrooms.WithShrooms => "exclude",
            _ => null,
        };
}

public enum TimeTrialVehicles
{
    All,
    Karts,
    Bikes,
}

public enum TimeTrialShrooms
{
    Any,
    ShroomlessOnly,
    WithShrooms,
}

public static class TimeTrials
{
    public const int PageSize = 25;

    /// <summary>The board's track for a ghost slot, matched by name (letters and digits only, ignoring case).</summary>
    public static RwfcTimeTrialTrack? BoardTrackFor(GhostTrack track, IEnumerable<RwfcTimeTrialTrack> boardTracks)
    {
        var wanted = Normalize(track.Name);
        return wanted.Length == 0 ? null : boardTracks.FirstOrDefault(board => !board.IsHidden && Normalize(board.Name) == wanted);
    }

    private static string Normalize(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    // Mario Kart Wii's own numbering, the same the board uses.
    private static readonly string[] Characters =
    [
        "Mario",
        "Baby Peach",
        "Waluigi",
        "Bowser",
        "Baby Daisy",
        "Dry Bones",
        "Baby Mario",
        "Luigi",
        "Toad",
        "Donkey Kong",
        "Yoshi",
        "Wario",
        "Baby Luigi",
        "Toadette",
        "Koopa Troopa",
        "Daisy",
        "Peach",
        "Birdo",
        "Diddy Kong",
        "King Boo",
        "Bowser Jr.",
        "Dry Bowser",
        "Funky Kong",
        "Rosalina",
    ];

    private static readonly string[] Vehicles =
    [
        "Standard Kart S",
        "Standard Kart M",
        "Standard Kart L",
        "Booster Seat",
        "Classic Dragster",
        "Offroader",
        "Mini Beast",
        "Wild Wing",
        "Flame Flyer",
        "Cheep Charger",
        "Super Blooper",
        "Piranha Prowler",
        "Tiny Titan",
        "Daytripper",
        "Jetsetter",
        "Blue Falcon",
        "Sprinter",
        "Honeycoupe",
        "Standard Bike S",
        "Standard Bike M",
        "Standard Bike L",
        "Bullet Bike",
        "Mach Bike",
        "Flame Runner",
        "Bit Bike",
        "Sugarscoot",
        "Wario Bike",
        "Quacker",
        "Zip Zip",
        "Shooting Star",
        "Magikruiser",
        "Sneakster",
        "Spear",
        "Jet Bubble",
        "Dolphin Dasher",
        "Phantom",
    ];

    /// <summary>"Funky Kong", or "Mii" for the Mii outfits (ids from 24 up).</summary>
    public static string CharacterName(int id) =>
        id >= 0 && id < Characters.Length ? Characters[id]
        : id is >= 24 and < 48 ? "Mii"
        : "?";

    public static string VehicleName(int id) => id >= 0 && id < Vehicles.Length ? Vehicles[id] : "?";

    /// <summary>The controller, numbered like on rwfc.net/tt.</summary>
    public static string ControllerName(int type) =>
        type switch
        {
            0 => "Wii Wheel",
            1 => "Nunchuk",
            2 => "Classic Controller",
            3 => "GameCube Controller",
            _ => "?",
        };

    /// <summary>"Hybrid Outside", "Manual Inside" and so on, like on rwfc.net/tt.</summary>
    public static string DriftName(int type, int category) =>
        $"{(type == 1 ? "Hybrid" : "Manual")} {(category == 1 ? "Inside" : "Outside")}";

    /// <summary>The second line of a time: character, vehicle, and what kind of run it was.</summary>
    public static string Details(RwfcTimeTrialTime time)
    {
        var parts = new List<string> { CharacterName(time.CharacterId), VehicleName(time.VehicleId) };
        if (time.Shroomless)
            parts.Add("Shroomless");
        if (time.Glitch)
            parts.Add("Glitch");
        if (!string.IsNullOrWhiteSpace(time.CountryAlpha2))
            parts.Add(time.CountryAlpha2!);
        return string.Join(" · ", parts);
    }
}
