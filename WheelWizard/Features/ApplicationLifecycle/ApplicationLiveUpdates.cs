using WheelWizard.DiscordPresence;
using WheelWizard.RrRooms;
using WheelWizard.WheelWizardData;

namespace WheelWizard.ApplicationLifecycle;

public interface IApplicationLiveUpdates
{
    void Start();
}

public sealed class ApplicationLiveUpdates(
    LiveStatusService status,
    LiveRoomsService rooms,
    DiscordPresenceService discord,
    RoomLinkListener roomLinks
) : IApplicationLiveUpdates
{
    public void Start()
    {
        status.Start();
        rooms.Start();
        discord.Start();
        roomLinks.Start();
    }
}
