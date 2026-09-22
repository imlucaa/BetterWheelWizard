namespace WheelWizard.Branding;

/// <summary>
/// Represents the branding information for the application.
/// </summary>
public class Branding
{
    /// <summary>
    /// The display name of the application.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The technical identifier for the application.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// The version of the application.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The URL of the repository for the application.
    /// </summary>
    public required Uri RepositoryUrl { get; init; }

    /// <summary>
    /// The URL of the Discord server for the application.
    /// </summary>
    public required Uri DiscordUrl { get; init; }

    /// <summary>
    /// The URL of the support page for the application.
    /// </summary>
    public required Uri SupportUrl { get; init; }
}
