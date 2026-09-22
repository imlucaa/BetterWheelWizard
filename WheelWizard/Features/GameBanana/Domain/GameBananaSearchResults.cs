using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaSearchResults
{
    /// <summary>
    /// Metadata for the API response (e.g., total records, pagination)
    /// </summary>
    [JsonPropertyName("_aMetadata")]
    public required GameBananaSearchMetaData MetaData { get; set; }

    /// <summary>
    ///  List of records representing mods or other GameBanana content
    /// </summary>
    [JsonPropertyName("_aRecords")]
    public required List<GameBananaModPreview> Records { get; set; }
}
