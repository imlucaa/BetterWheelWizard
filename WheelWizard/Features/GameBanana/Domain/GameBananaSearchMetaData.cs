using System.Text.Json.Serialization;

namespace WheelWizard.GameBanana.Domain;

public class GameBananaSearchMetaData
{
    /// <summary>
    /// Number of records found in total given the search parameters
    /// </summary>
    [JsonPropertyName("_nRecordCount")]
    public required int RecordCount { get; set; }

    /// <summary>
    /// Number of records per page returned by the API
    /// </summary>
    [JsonPropertyName("_nPerpage")]
    public required int PerPage { get; set; }

    [JsonPropertyName("_bIsComplete")]
    public required bool IsComplete { get; set; }
}
