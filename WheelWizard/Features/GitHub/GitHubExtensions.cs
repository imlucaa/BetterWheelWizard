using System.Text.Json;
using WheelWizard.GitHub.Domain;
using WheelWizard.Services;

namespace WheelWizard.GitHub;

public static class GitHubExtensions
{
    public static IServiceCollection AddGitHub(this IServiceCollection services)
    {
        services.AddWhWzRefitApi<IGitHubApi>(Endpoints.GitHubAddress, new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        services.AddSingleton<IGitHubSingletonService, GitHubSingletonService>();
        return services;
    }
}
