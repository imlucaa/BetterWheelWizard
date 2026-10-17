using System.Globalization;
using WheelWizard.RrRooms.Views;

namespace WheelWizard.Test.Views;

public class RoomsPageTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("not a number", null)]
    [InlineData("-1", null)]
    [InlineData("0", 0)]
    [InlineData("9999", 9999)]
    public void ParseVrFilter_AcceptsOnlyNonNegativeNumbers(string? input, int? expected)
    {
        Assert.Equal(expected, RoomsPage.ParseVrFilter(input));
    }

    [Fact]
    public void ParseVrFilter_AcceptsCurrentCultureThousandsSeparators()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(12_345, RoomsPage.ParseVrFilter("12,345"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
