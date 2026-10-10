using WheelWizard.Themes;

namespace WheelWizard.Test.Features;

public class LauncherBackgroundTests
{
    [Fact]
    public void Decode_AnimatedGif_ReportsMultipleFrames()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "animated.gif");

        Assert.True(LauncherBackgroundImage.GetAnimatedFrameCount(fixture) > 1);
    }
}
