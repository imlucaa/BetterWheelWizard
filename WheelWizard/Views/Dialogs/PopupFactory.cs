using WheelWizard.Views.Dialogs.Base;

namespace WheelWizard.Views.Dialogs;

public interface IPopupFactory
{
    T Create<T>(params object[] arguments)
        where T : PopupContent;
}

/// <summary>Constructs popup content with its services and explicit caller-supplied arguments.</summary>
public sealed class PopupFactory(IServiceProvider services) : IPopupFactory
{
    public T Create<T>(params object[] arguments)
        where T : PopupContent => ActivatorUtilities.CreateInstance<T>(services, arguments);
}

public static class PopupFactoryExtensions
{
    public static ThemeLibraryWindow CreateThemeLibrary(this IPopupFactory factory, IReadOnlyList<string> themeNames) =>
        factory.Create<ThemeLibraryWindow>((object)themeNames);
}
