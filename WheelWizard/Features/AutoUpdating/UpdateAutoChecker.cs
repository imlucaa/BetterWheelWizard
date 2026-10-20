using Microsoft.Extensions.Logging;
using WheelWizard.Settings;
using WheelWizard.Shared.Polling;

namespace WheelWizard.AutoUpdating;

/// <summary>
/// Looks for a new release every few hours while the launcher stays open, without opening any pop-up. A found update
/// raises <see cref="IAutoUpdaterSingletonService.UpdateAvailable"/>, which lights up the dot on the sidebar. The check
/// at startup already ran, so the first tick of this service does nothing.
/// </summary>
public sealed class UpdateAutoChecker : ObservablePollingService
{
    public const double HoursBetweenChecks = 6;

    private readonly IAutoUpdaterSingletonService _updater;
    private readonly ISettingsManager _settings;
    private bool _skippedFirst;

    public UpdateAutoChecker(
        IAutoUpdaterSingletonService updater,
        ISettingsManager settings,
        IPollingScheduler scheduler,
        TimeProvider timeProvider,
        ILogger<UpdateAutoChecker> logger
    )
        : base(TimeSpan.FromHours(HoursBetweenChecks).TotalSeconds, scheduler, timeProvider, logger)
    {
        _updater = updater;
        _settings = settings;
    }

    protected override async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        if (!_skippedFirst)
        {
            _skippedFirst = true;
            return;
        }

        if (!_settings.AUTO_CHECK_UPDATES.Get() || _updater.IsUpdateAvailable)
            return;

        await _updater.CheckNowAsync();
    }
}
