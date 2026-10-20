using Microsoft.Extensions.Logging;
using WheelWizard.AutoUpdating;
using WheelWizard.Settings;
using WheelWizard.Settings.Types;
using WheelWizard.Shared.Polling;

namespace WheelWizard.Test.Features.AutoUpdating;

public class UpdateAutoCheckerTests
{
    [Fact]
    public async Task AutoChecker_SkipsTheFirstTick_ThenChecksOnlyWhenAllowed()
    {
        var updater = Substitute.For<IAutoUpdaterSingletonService>();
        var settings = Substitute.For<ISettingsManager>();
        var allowed = new WhWzSetting<bool>("AutoCheckUpdates", true);
        settings.AUTO_CHECK_UPDATES.Returns(allowed);
        using var checker = new UpdateAutoChecker(
            updater,
            settings,
            Substitute.For<IPollingScheduler>(),
            TimeProvider.System,
            Substitute.For<ILogger<UpdateAutoChecker>>()
        );

        checker.Start();
        await checker.InitialUpdate;
        await updater.DidNotReceive().CheckNowAsync();

        // The scheduler is a stand-in, so the next tick is driven by hand through the same protected entry point.
        await InvokeTickAsync(checker);
        await updater.Received(1).CheckNowAsync();

        allowed.Set(false);
        await InvokeTickAsync(checker);
        await updater.Received(1).CheckNowAsync();
    }

    [Fact]
    public async Task AutoChecker_DoesNotCheckAgainOnceAnUpdateIsKnown()
    {
        var updater = Substitute.For<IAutoUpdaterSingletonService>();
        updater.IsUpdateAvailable.Returns(true);
        var settings = Substitute.For<ISettingsManager>();
        settings.AUTO_CHECK_UPDATES.Returns(new WhWzSetting<bool>("AutoCheckUpdates", true));
        using var checker = new UpdateAutoChecker(
            updater,
            settings,
            Substitute.For<IPollingScheduler>(),
            TimeProvider.System,
            Substitute.For<ILogger<UpdateAutoChecker>>()
        );
        checker.Start();
        await checker.InitialUpdate;

        await InvokeTickAsync(checker);

        await updater.DidNotReceive().CheckNowAsync();
    }

    private static Task InvokeTickAsync(UpdateAutoChecker checker) =>
        (Task)
            typeof(UpdateAutoChecker)
                .GetMethod("ExecuteTaskAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(checker, [CancellationToken.None])!;
}
