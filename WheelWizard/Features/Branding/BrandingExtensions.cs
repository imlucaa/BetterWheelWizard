namespace WheelWizard.Branding;

public static class BrandingExtensions
{
    public static IServiceCollection AddBranding(this IServiceCollection services)
    {
        services.AddSingleton<IBrandingSingletonService, BrandingSingletonService>();

        return services;
    }
}
