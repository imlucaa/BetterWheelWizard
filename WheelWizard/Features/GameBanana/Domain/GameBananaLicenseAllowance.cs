using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaLicenseAllowance
{
    // yes it is really true that this class does not prefix it anymore with _x
    // ask GameBanana why

    [JsonPropertyName("yes")]
    public required List<String> Allowed { get; set; }

    [JsonPropertyName("ask")]
    public required List<String> OnRequest { get; set; }

    [JsonPropertyName("no")]
    public required List<String> NotAllowed { get; set; }
}
