using Avalonia.Controls;
using Serilog;

namespace WheelWizard.Views;

public static class NavigationManager
{
    public static bool NavigateTo(Type pageType, params object?[] args)
    {
        try
        {
            if (Activator.CreateInstance(pageType, args) is not UserControl instance)
                throw new InvalidOperationException($"Failed to create an instance of {pageType.FullName}");

            Layout.Instance.NavigateToPage(instance);
            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to navigate to page {PageType}", pageType.FullName);
            ViewUtils.ShowSnackbar($"Could not open {GetPageName(pageType)}.", ViewUtils.SnackbarType.Danger);
            return false;
        }
    }

    public static bool NavigateTo<T>(params object?[] args)
        where T : UserControlBase => NavigateTo(typeof(T), args);

    private static string GetPageName(Type pageType) =>
        pageType.Name.EndsWith("Page", StringComparison.Ordinal) ? pageType.Name[..^4] : pageType.Name;
}
