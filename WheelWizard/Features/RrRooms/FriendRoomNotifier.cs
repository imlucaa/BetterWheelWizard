using Avalonia.Threading;
using WheelWizard.Models.RRInfo;
using WheelWizard.Settings;
using WheelWizard.Shared.Polling;
using WheelWizard.Views.Shell;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.RrRooms;

/// <summary>
/// Works out which friends have just appeared in a room since the last room list. The first list only becomes the
/// baseline, so friends already in a room when the launcher opens do not trigger anything.
/// </summary>
public sealed class FriendRoomDetector
{
    private HashSet<string>? _previous;

    /// <summary>Names of the friends that are in a room now but were not in the previous list.</summary>
    public IReadOnlyList<string> Update(IEnumerable<RrRoom> rooms, IReadOnlySet<string> friendCodes)
    {
        var current = new Dictionary<string, string>();
        foreach (var player in rooms.SelectMany(room => room.Players))
        {
            var code = DigitsOf(player.FriendCode);
            if (code.Length > 0 && friendCodes.Contains(code))
                current[code] = player.Name;
        }

        var previous = _previous;
        _previous = [.. current.Keys];
        if (previous is null)
            return [];

        return current.Where(entry => !previous.Contains(entry.Key)).Select(entry => entry.Value).ToList();
    }

    public static string DigitsOf(string? friendCode) => new((friendCode ?? string.Empty).Where(char.IsDigit).ToArray());
}

/// <summary>Shows a small message when one of the selected license's friends joins a room.</summary>
public sealed class FriendRoomNotifier : IPollingListener
{
    private readonly LiveRoomsService _rooms;
    private readonly IGameLicenseSingletonService _licenses;
    private readonly ISettingsManager _settings;
    private readonly FriendRoomDetector _detector = new();
    private bool _started;

    public FriendRoomNotifier(LiveRoomsService rooms, IGameLicenseSingletonService licenses, ISettingsManager settings)
    {
        _rooms = rooms;
        _licenses = licenses;
        _settings = settings;
    }

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _rooms.Subscribe(this);
    }

    public void OnUpdate(ObservablePollingService sender)
    {
        if (sender is not LiveRoomsService rooms)
            return;

        IReadOnlySet<string> friendCodes;
        try
        {
            friendCodes = _licenses.ActiveCurrentFriends.Select(friend => FriendRoomDetector.DigitsOf(friend.FriendCode)).ToHashSet();
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or NullReferenceException)
        {
            // No license is loaded yet. Try again with the next room list.
            return;
        }

        // Always track the rooms, even while alerts are off, so turning them on later does not announce everyone at once.
        var joined = _detector.Update(rooms.CurrentRooms, friendCodes);
        if (joined.Count == 0 || !_settings.FRIEND_ROOM_ALERTS.Get())
            return;

        var message =
            joined.Count == 1 ? $"{joined[0]} joined a room." : $"{joined.Count} friends joined rooms: {string.Join(", ", joined)}.";
        Dispatcher.UIThread.Post(() => ViewUtils.ShowSnackbar(message));
    }
}
