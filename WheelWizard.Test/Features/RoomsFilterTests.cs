using WheelWizard.Views.Pages;

namespace WheelWizard.Test.Features;

public class RoomsFilterTests
{
    [Theory]
    [InlineData("20794", 20794)]
    [InlineData("20,794", 20794)]
    [InlineData(" 22,989 ", 22989)]
    public void ParseVrFilter_AcceptsDisplayedVrFormatting(string text, int expected)
    {
        Assert.Equal(expected, RoomsPage.ParseVrFilter(text));
    }
}
