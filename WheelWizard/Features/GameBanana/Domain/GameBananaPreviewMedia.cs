using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaPreviewMedia
{
    /// <summary>
    /// Not all previews have this
    /// </summary>
    [JsonPropertyName("_aMetadata")]
    public GameBananaPreviewMetaData? MetaData { get; set; }

    [JsonPropertyName("_aImages")]
    public List<GameBananaImage> Images { get; set; } = [];
}
