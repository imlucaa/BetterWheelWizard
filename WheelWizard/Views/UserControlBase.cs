using Avalonia.Controls;
using WheelWizard.Shared.DependencyInjection;

namespace WheelWizard.Views;

public abstract class UserControlBase : UserControl
{
    protected IServiceProvider ServiceProvider { get; }

    protected UserControlBase()
    {
        ServiceProvider = App.Services;
        ServiceInjector.InjectServices(ServiceProvider, this);
    }
}
