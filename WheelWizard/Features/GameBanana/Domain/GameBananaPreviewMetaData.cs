using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaPreviewMetaData
{
    [JsonPropertyName("_Snippet")]
    public string? DescriptionSnippet { get; set; }
}
