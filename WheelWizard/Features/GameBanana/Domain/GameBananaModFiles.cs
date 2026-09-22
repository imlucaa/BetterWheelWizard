using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaModFiles
{
    [JsonPropertyName("_sFile")]
    public required string FileName { get; set; }

    [JsonPropertyName("_nFilesize")]
    public int FileSize { get; set; }

    [JsonPropertyName("_sDownloadUrl")]
    public required string DownloadUrl { get; set; }
}
