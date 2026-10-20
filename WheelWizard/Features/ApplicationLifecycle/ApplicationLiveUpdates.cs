using WheelWizard.AutoUpdating;
using WheelWizard.DiscordPresence;
using WheelWizard.RrRooms;
using WheelWizard.WheelWizardData;
using WheelWizard.WiiManagement.GameLicense;

namespace WheelWizard.ApplicationLifecycle;

public interface IApplicationLiveUpdates
{
    void Start();
}

public sealed class ApplicationLiveUpdates(
    LiveStatusService status,
    LiveRoomsService rooms,
    DiscordPresenceService discord,
    RoomLinkListener roomLinks,
    FriendRoomNotifier friendRooms,
    SessionStatsTracker sessionStats,
    UpdateAutoChecker updateChecker
) : IApplicationLiveUpdates
{
    public void Start()
    {
        status.Start();
        rooms.Start();
        discord.Start();
        roomLinks.Start();
        friendRooms.Start();
        sessionStats.Start();
        updateChecker.Start();
    }
}
