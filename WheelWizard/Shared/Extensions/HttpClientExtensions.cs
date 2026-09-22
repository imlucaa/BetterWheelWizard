using WheelWizard.Branding;

namespace WheelWizard.Shared;

public static class HttpClientExtensions
{
    /// <summary>
    /// Configures the HttpClient to use WheelWizard conventions.
    /// </summary>
    public static void ConfigureWheelWizardClient(this HttpClient client, IServiceProvider serviceProvider)
    {
        var branding = serviceProvider.GetRequiredService<IBrandingSingletonService>().Branding;
        client.DefaultRequestHeaders.UserAgent.Add(new(branding.Identifier, branding.Version));
    }
}
