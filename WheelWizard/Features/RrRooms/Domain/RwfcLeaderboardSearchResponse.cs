namespace WheelWizard.RrRooms;

public sealed class RwfcLeaderboardSearchResponse
{
    public List<RwfcLeaderboardEntry> Players { get; set; } = [];
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
}
