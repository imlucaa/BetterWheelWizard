using System.IO.Abstractions;
using System.Runtime.InteropServices;
using WheelWizard.AutoUpdating.Platforms;
using WheelWizard.GitHub.Domain;

namespace WheelWizard.Test.Features;

public class AutoUpdateAssetTests
{
    [Fact]
    public void WindowsUpdater_PrefersBrandedReleaseAsset()
    {
        var release = CreateRelease("unrelated.exe", "WheelWizardWindows.exe", "BetterWheelWizardWindows.exe");

        var platform = new WindowsUpdatePlatform(Substitute.For<IFileSystem>());

        Assert.Equal("BetterWheelWizardWindows.exe", platform.GetAssetForCurrentPlatform(release)?.Name);
    }

    [Fact]
    public void WindowsUpdater_RemainsCompatibleWithLegacyAssetName()
    {
        var release = CreateRelease("WheelWizardWindows.exe");
        var platform = new WindowsUpdatePlatform(Substitute.For<IFileSystem>());

        Assert.Equal("WheelWizardWindows.exe", platform.GetAssetForCurrentPlatform(release)?.Name);
    }

    [Theory]
    [InlineData(Architecture.X64, "BetterWheelWizard_Linux")]
    [InlineData(Architecture.Arm64, "BetterWheelWizard_ARM64_Linux")]
    public void LinuxUpdater_SelectsBrandedAssetForArchitecture(Architecture architecture, string expected)
    {
        var release = CreateRelease("BetterWheelWizard_Linux", "BetterWheelWizard_ARM64_Linux");

        Assert.Equal(expected, LinuxUpdatePlatform.GetAssetForArchitecture(release, architecture)?.Name);
    }

    [Theory]
    [InlineData(Architecture.X64, "WheelWizard_Linux")]
    [InlineData(Architecture.Arm64, "WheelWizard_arm64_Linux")]
    public void LinuxUpdater_RemainsCompatibleWithLegacyAssetNames(Architecture architecture, string expected)
    {
        var release = CreateRelease("WheelWizard_Linux", "WheelWizard_arm64_Linux");

        Assert.Equal(expected, LinuxUpdatePlatform.GetAssetForArchitecture(release, architecture)?.Name);
    }

    private static GithubRelease CreateRelease(params string[] assetNames) =>
        new()
        {
            TagName = "v2.5.11",
            Assets = [.. assetNames.Select(name => new GithubAsset { Name = name, BrowserDownloadUrl = $"https://example.com/{name}" })],
        };
}
