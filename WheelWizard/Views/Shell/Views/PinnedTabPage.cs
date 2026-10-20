using Avalonia;
using Avalonia.Controls;
using WheelWizard.Views.Shell.Navigation;

namespace WheelWizard.Views.Shell.Views;

/// <summary>
/// A settings tab pinned to the sidebar, shown as a page of its own (not inside Settings), so the sidebar stays as it is
/// and highlights the pinned tab.
/// </summary>
public sealed class PinnedTabPage : UserControl
{
    public Type Tab { get; }

    public PinnedTabPage(IPageFactory pages, Type tab)
    {
        Tab = tab;
        Content = new Border { Padding = new Thickness(24, 34, 24, 0), Child = pages.Create(tab) };
    }
}
