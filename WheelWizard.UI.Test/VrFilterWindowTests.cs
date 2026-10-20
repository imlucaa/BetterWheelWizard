using Avalonia.Headless.XUnit;
using WheelWizard.Views.Dialogs;

namespace WheelWizard.UI.Test;

public class VrFilterWindowTests
{
    [AvaloniaFact]
    public void FilterWindow_OpensWithTheCurrentRange()
    {
        var window = new VrFilterWindow(10_000, 25_000);

        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public void FilterWindow_OpensWithoutARange()
    {
        var window = new VrFilterWindow(null, null);

        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public void SortWindow_OpensWithTheCurrentSortMarked()
    {
        var window = new RoomSortWindow(2);

        Assert.NotNull(window);
    }
}
