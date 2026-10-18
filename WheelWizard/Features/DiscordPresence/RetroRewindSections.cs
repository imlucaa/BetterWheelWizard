namespace WheelWizard.DiscordPresence;

/// <summary>
/// What the game is showing, by Mario Kart Wii's section id. The ids and wording are the ones Retro Rewind's own
/// presence uses (rr-pulsar, PulsarEngine/Extra/Discord.cpp), so the launcher shows the same status.
/// </summary>
public static class RetroRewindSections
{
    private const int FirstWifi = 85;
    private const int LastWifi = 119;
    private const string Menu = "In a Menu";

    private static readonly Dictionary<int, string> Names = new()
    {
        [30] = "In a Grand Prix",
        [31] = "In Time Trials",
        [32] = "In a 1P VS",
        [33] = "In a 2P VS",
        [34] = "In a 3P VS",
        [35] = "In a 4P VS",
        [36] = "In a 1P Team VS",
        [37] = "In a 2P Team VS",
        [38] = "In a 3P Team VS",
        [39] = "In a 4P Team VS",
        [40] = "In a 1P Battle",
        [41] = "In a 2P Battle",
        [42] = "In a 3P Battle",
        [43] = "In a 4P Battle",
        [44] = "In Mission Mode",
        [45] = "In a Tournament",
        [46] = "Watching a GP Replay",
        [47] = "Watching a TT Replay",
        [50] = "Watching a TT Replay",
        [51] = "Watching a TT Replay",
        [52] = "Watching a TT Replay",
        [85] = "In a WiFi menu",
        [86] = "In a WiFi menu",
        [87] = "In a WiFi menu",
        [88] = "Voting for a WiFi VS",
        [89] = "Voting for a WiFi Battle",
        [91] = "In a WiFi menu",
        [92] = "In a WiFi menu",
        [93] = "In a WiFi menu",
        [94] = "Voting for a WiFi VS",
        [95] = "Voting for a WiFi Battle",
        [96] = "Voting for a VS in a froom",
        [97] = "Voting for a Team VS in a froom",
        [98] = "Voting for a Balloon Battle in a froom",
        [99] = "Voting for a Coin Runners in a froom",
        [100] = "Voting for a VS in a froom",
        [101] = "Voting for a Team VS in a froom",
        [102] = "Voting for a Balloon Battle in a froom",
        [103] = "Voting for a Coin Runners in a froom",
        [104] = "Racing in a WiFi VS",
        [105] = "Racing in a WiFi VS",
        [106] = "Spectating a WiFi VS",
        [107] = "Spectating a WiFi VS",
        [108] = "Racing in a WiFi Battle",
        [109] = "Racing in a WiFi Battle",
        [110] = "Spectating a WiFi Battle",
        [111] = "Spectating a WiFi Battle",
        [112] = "Racing in a WiFi Friend VS",
        [113] = "Racing in a WiFi Friend Team VS",
        [114] = "Racing in a WiFi Friend Balloon Battle",
        [115] = "Racing in a WiFi Friend Coin Runners",
        [116] = "Racing in a WiFi Friend VS",
        [117] = "Racing in a WiFi Friend Team VS",
        [118] = "Racing in a WiFi Friend Balloon Battle",
        [119] = "Racing in a WiFi Friend Coin Runners",
    };

    public static string Describe(int sectionId) => Names.GetValueOrDefault(sectionId, Menu);

    /// <summary>The game also appends the player's VR and BR on every online screen.</summary>
    public static bool IsOnline(int sectionId) => sectionId is >= FirstWifi and <= LastWifi;

    /// <summary>Online screens where a track is actually being raced or watched (not menus or voting).</summary>
    public static bool IsOnlineRace(int sectionId) => sectionId is >= 104 and <= LastWifi;
}
