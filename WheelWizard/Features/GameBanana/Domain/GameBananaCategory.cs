using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaCategory
{
    /// <summary>
    /// e.g., "Maps", "Characters"
    /// </summary>
    [JsonPropertyName("_sName")]
    public required string Name { get; set; }

    [JsonPropertyName("_sProfileUrl")]
    public required string ProfileUrl { get; set; }

    /// <summary>
    /// Unsure if all categories have an icon, and don't want risk adding `required` here
    /// </summary>
    [JsonPropertyName("_sIconUrl")]
    public string? IconUrl { get; set; }
}
