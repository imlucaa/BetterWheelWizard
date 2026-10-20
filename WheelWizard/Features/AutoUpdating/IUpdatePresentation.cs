using WheelWizard.Shared.Downloads;

namespace WheelWizard.AutoUpdating;

public interface IUpdatePresentation
{
    Task<bool> ConfirmUpdateAsync(string latestVersion, string currentVersion);

    /// <summary>The same question, with a short summary of what the new version changes.</summary>
    Task<bool> ConfirmUpdateAsync(string latestVersion, string currentVersion, string releaseNotes);
    Task<bool> ConfirmElevationAsync();
    Task ShowCheckFailureAsync(string message);
    Task ShowUpdateFailureAsync(string message);
    Task ShowManualUpdateAsync(string latestVersion, string currentVersion);
    Task<OperationResult> RunUpdateAsync(Func<IProgress<DownloadProgress>, CancellationToken, Task<OperationResult>> operation);
}
