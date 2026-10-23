namespace WheelWizard.Views.Shell;

/// <summary>A page whose sidebar button is hidden, with the button's name.</summary>
public sealed record HiddenSidebarTab(string Label, Type Page);

/// <summary>
/// The pages hidden from the sidebar, so Settings can still offer them: hiding a tab tidies the sidebar, it should not
/// lock the page away. The sidebar fills this in every time it is arranged.
/// </summary>
public static class HiddenSidebarTabs
{
    public static IReadOnlyList<HiddenSidebarTab> Current { get; private set; } = [];

    public static event Action? Changed;

    public static void Set(IReadOnlyList<HiddenSidebarTab> tabs)
    {
        if (tabs.SequenceEqual(Current))
            return;
        Current = tabs;
        Changed?.Invoke();
    }
}
