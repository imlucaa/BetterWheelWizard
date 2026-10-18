using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using NSubstitute;
using WheelWizard.DiscordPresence;
using WheelWizard.Settings;
using WheelWizard.Views.Dialogs;

namespace WheelWizard.UI.Test;

public class DiscordSetupWindowTests
{
    [AvaloniaFact]
    public void SetupWindow_LoadsItsLayout()
    {
        var settings = Substitute.For<ISettingsManager>();
        // The window only needs the service to exist until Finish is pressed.
        var presence = (DiscordPresenceService)RuntimeHelpers.GetUninitializedObject(typeof(DiscordPresenceService));

        var window = new DiscordSetupWindow(settings, presence);

        Assert.NotNull(window);
    }
}
