using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NSubstitute;
using WheelWizard.Ghosts.Views;
using WheelWizard.RrLounge.Views;
using WheelWizard.Settings;
using WheelWizard.Settings.Views;
using WheelWizard.Views.Components;
using WheelWizard.Views.Dialogs;
using WheelWizard.Views.Shell;
using WheelWizard.Views.Shell.Navigation;

namespace WheelWizard.UI.Test;

public class HiddenSidebarTabsTests
{
    [AvaloniaFact]
    public void TabsHiddenFromTheSidebar_AreListedInSettings_AndOpenFromThere()
    {
        var pages = Substitute.For<IPageFactory>();
        pages.Create(Arg.Any<Type>(), Arg.Any<object[]>()).Returns(_ => new UserControl());
        var navigation = Substitute.For<INavigationService>();
        HiddenSidebarTabs.Set([new("RR Lounge", typeof(RrLoungePage)), new("Ghosts", typeof(GhostsPage))]);
        try
        {
            var settings = new SettingsPage(
                Substitute.For<IPopupFactory>(),
                Substitute.For<ISettingsManager>(),
                Substitute.For<ISettingsSignalBus>(),
                pages,
                navigation
            );
            var window = new Window { Content = settings };
            window.Show();

            Assert.True(settings.FindControl<StackPanel>("HiddenTabsPanel")!.IsVisible);
            var hidden = settings.FindControl<SegmentedControl>("HiddenPages")!;
            Assert.Equal(["RR Lounge", "Ghosts"], hidden.Items.OfType<SegmentOption>().Select(option => option.Text));

            hidden.SelectedIndex = 1;
            navigation.Received(1).NavigateTo(typeof(GhostsPage));
            // Nothing stays selected, so the same tab can be opened again.
            Assert.Null(hidden.SelectedItem);

            // Showing every tab again empties the list.
            HiddenSidebarTabs.Set([]);
            Assert.False(settings.FindControl<StackPanel>("HiddenTabsPanel")!.IsVisible);
            window.Close();
        }
        finally
        {
            HiddenSidebarTabs.Set([]);
        }
    }
}
