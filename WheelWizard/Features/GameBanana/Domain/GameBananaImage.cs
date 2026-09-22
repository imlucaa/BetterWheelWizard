using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaImage
{
    /// <summary>
    /// media type (e.g., "screenshot")
    /// </summary>
    [JsonPropertyName("_sType")]
    public required string Type { get; set; }

    [JsonPropertyName("_sBaseUrl")]
    public required string BaseUrl { get; set; }

    [JsonPropertyName("_sFile")]
    public required string File { get; set; }

    /// <summary>
    /// Note that the original image can also  be referred to as _sFile100
    /// Sometimes there are higher resolution images available
    /// </summary>
    [JsonPropertyName("_sFile220")]
    public string? File220 { get; set; }

    /// <summary>
    /// Note that the original image can also  be referred to as _sFile100
    /// Sometimes there are higher resolution images available
    /// </summary>
    [JsonPropertyName("_sFile530")]
    public string? File530 { get; set; }
}
