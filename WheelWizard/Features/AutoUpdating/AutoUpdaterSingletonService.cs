using Avalonia.Threading;
using Semver;
using WheelWizard.AutoUpdating.Platforms;
using WheelWizard.Branding;
using WheelWizard.GitHub;
using WheelWizard.GitHub.Domain;
using WheelWizard.Helpers;
using WheelWizard.Views.Popups.Generic;

namespace WheelWizard.AutoUpdating;

public interface IAutoUpdaterSingletonService
{
    public Task CheckForUpdatesAsync();
}

public class AutoUpdaterSingletonService(
    IUpdatePlatform updatePlatform,
    IBrandingSingletonService brandingService,
    IGitHubSingletonService gitHubService
) : IAutoUpdaterSingletonService
{
    private string CurrentVersion => brandingService.Branding.Version;

    public async Task CheckForUpdatesAsync()
    {
        // TODO: How to run this in a background thread?
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease?.TagName is null)
            return;

        var asset = updatePlatform.GetAssetForCurrentPlatform(latestRelease);
        if (asset is null)
            return;

        var latestVersion = SemVersion.Parse(latestRelease.TagName.TrimStart('v'), SemVersionStyles.Any);
        var popupExtraText = t("question.new_version_wh_wz.extra", latestVersion, CurrentVersion)!;

        var shouldUpdate = false;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            shouldUpdate = await new YesNoWindow()
                .SetButtonText(t("action.update"), t("action.maybe_later"))
                .SetMainText(t("question.new_version_wh_wz.title"))
                .SetExtraText(popupExtraText)
                .AwaitAnswer();
        });

        if (!shouldUpdate)
            return;

        var updateResult = await updatePlatform.ExecuteUpdateAsync(asset.BrowserDownloadUrl);

        if (updateResult.IsFailure)
        {
            await new MessageBoxWindow()
                .SetMessageType(MessageBoxWindow.MessageType.Warning)
                .SetTitleText("Unable to update Wheel Wizard")
                .SetInfoText(updateResult.Error.Message)
                .ShowDialog();
        }
    }

    private async Task<GithubRelease?> GetLatestReleaseAsync()
    {
        var releasesResult = await gitHubService.GetReleasesAsync();
        if (releasesResult.IsFailure)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await new MessageBoxWindow()
                    .SetMessageType(MessageBoxWindow.MessageType.Error)
                    .SetTitleText("Failed to check for updates")
                    .SetInfoText(
                        "An error occurred while checking for updates. Please try again later. "
                            + "\nError: "
                            + releasesResult.Error.Message
                    )
                    .ShowDialog();
            });

            return null;
        }

        if (releasesResult.Value.Count == 0)
            return null;

        // GitHub returns releases newest-first. Respect that publication order instead of
        // selecting the numerically highest historical tag: this fork can publish a newer
        // 2.5.x release after an older experimental 2.6.x release.
        var currentVersion = SemVersion.Parse(CurrentVersion, SemVersionStyles.Any);

        foreach (var release in releasesResult.Value)
        {
            if (release.TagName == null!)
                continue;

            if (release.Prerelease)
                continue;

            var releaseVersion = SemVersion.Parse(release.TagName.TrimStart('v'), SemVersionStyles.Any);
            var asset = updatePlatform.GetAssetForCurrentPlatform(release);
            if (asset is null)
                continue;

            // This is the newest published release with a usable asset. Do not continue
            // into older releases if it is already installed; historical tags may have
            // higher experimental version numbers.
            return releaseVersion.ComparePrecedenceTo(currentVersion) > 0 ? release : null;
        }

        return null;
    }
}
