using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaGame
{
    [JsonPropertyName("_sName")]
    public required string Name { get; set; }

    [JsonPropertyName("_sProfileUrl")]
    public required string ProfileUrl { get; set; }

    [JsonPropertyName("_sIconUrl")]
    public required string IconUrl { get; set; }
}
