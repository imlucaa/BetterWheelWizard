using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Refit;
using WheelWizard.Shared.Services;

namespace WheelWizard.RrLounge;

/// <summary>The RR Lounge (rr-lounge.com) leaderboard: competitive Retro Rewind mogis, ranked by MMR.</summary>
public interface IRrLoungeApi
{
    /// <summary>Rank, name and MMR of every player in a ladder.</summary>
    [Get("/api/leaderboard")]
    Task<RrLoungeBoard> GetLeaderboardAsync([AliasAs("category")] string category, [AliasAs("season")] string? season);

    /// <summary>The leaderboard web page, which also carries each player's stats and the list of seasons.</summary>
    [Get("/leaderboard")]
    Task<string> GetLeaderboardPageAsync([AliasAs("category")] string category, [AliasAs("season")] string? season);

    /// <summary>A player's page: their division, stats and every mogi of the season.</summary>
    [Get("/players/{slug}")]
    Task<string> GetPlayerPageAsync(string slug, [AliasAs("category")] string category, [AliasAs("season")] string? season);
}

public sealed class RrLoungeBoard
{
    public string Category { get; set; } = string.Empty;
    public RrLoungeSeason? Season { get; set; }
    public int PlayerCount { get; set; }
    public List<RrLoungePlayer> Players { get; set; } = [];
}

public sealed class RrLoungeSeason
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class RrLoungePlayer
{
    public int Rank { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public int Mmr { get; set; }
    public int? PeakMmr { get; set; }
    public int? MogiCount { get; set; }
    public double? WinPercentage { get; set; }
    public int? Last10Wins { get; set; }
    public int? Last10Losses { get; set; }
    public double? AverageScore { get; set; }
    public double? AverageScoreLast10 { get; set; }
    public double? PartnerAverageScore { get; set; }

    public bool HasStats => MogiCount is not null;
}

/// <summary>An MMR rank (Bronze, Gold, Master…) and the colour the site paints MMR in.</summary>
public sealed class RrLoungeTier
{
    public string Name { get; set; } = string.Empty;
    public int MinMmr { get; set; }
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>The rank's badge on the site, such as "/icons/new_diamond.png".</summary>
    public string? Icon { get; set; }
}

/// <summary>Everything the leaderboard page shows for one ladder and season.</summary>
public sealed record RrLoungeLadderData(
    List<RrLoungePlayer> Players,
    List<RrLoungeTier> Tiers,
    List<RrLoungeSeasonLink> Seasons,
    string? Season
);

/// <summary>A season the site lists for a ladder, with the value its <c>season</c> query takes.</summary>
public sealed record RrLoungeSeasonLink(string Slug, string Name);

/// <summary>The three ladders, as the site names them.</summary>
public enum RrLoungeLadder
{
    Rr,
    Ct,
    Tt,
}

public static partial class RrLoungeData
{
    public const string SiteUrl = "https://rr-lounge.com";

    public static string Query(RrLoungeLadder ladder) =>
        ladder switch
        {
            RrLoungeLadder.Ct => "ct",
            RrLoungeLadder.Tt => "tt",
            _ => "rr",
        };

    public static string PageUrl(RrLoungeLadder ladder, string? season) =>
        $"{SiteUrl}/leaderboard?category={Query(ladder)}" + (season is null ? "" : $"&season={Uri.EscapeDataString(season)}");

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The players with their stats, read from the page. The page is a Next.js page, so the data sits in its script as
    /// escaped JSON. Returns an empty list when the page changes shape; the API's rank and MMR still work then.
    /// </summary>
    public static List<RrLoungePlayer> ParseStats(string? html) => ParseArray<RrLoungePlayer>(html, "\"players\":[{\"id\"");

    /// <summary>The season's MMR ranks, lowest first, read from the page.</summary>
    public static List<RrLoungeTier> ParseTiers(string? html) =>
        ParseArray<RrLoungeTier>(html, "\"rankTiers\":[").OrderBy(tier => tier.MinMmr).ToList();

    private static List<T> ParseArray<T>(string? html, string marker)
    {
        if (string.IsNullOrEmpty(html))
            return [];
        var text = html.Replace("\\\"", "\"");
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return [];
        start = text.IndexOf('[', start);
        var end = ArrayEnd(text, start);
        if (end < 0)
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(text[start..(end + 1)], Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Ranks the players like the site: highest MMR first, ties by name.</summary>
    public static List<RrLoungePlayer> Ranked(IEnumerable<RrLoungePlayer> players)
    {
        var ranked = players.OrderByDescending(player => player.Mmr).ThenBy(player => player.Name, StringComparer.Ordinal).ToList();
        for (var i = 0; i < ranked.Count; i++)
            ranked[i].Rank = i + 1;
        return ranked;
    }

    /// <summary>The highest rank whose minimum the MMR reaches, or null with no ranks known.</summary>
    public static RrLoungeTier? TierFor(int? mmr, IReadOnlyList<RrLoungeTier> tiers) =>
        mmr is { } value ? tiers.LastOrDefault(tier => value >= tier.MinMmr) ?? tiers.FirstOrDefault() : null;

    /// <summary>The player's page on the site, the same link the site's table uses.</summary>
    public static string PlayerUrl(string name, RrLoungeLadder ladder, string? season) =>
        $"{SiteUrl}/players/{PlayerSlug(name)}?category={Query(ladder)}"
        + (season is null ? "" : $"&season={Uri.EscapeDataString(season)}");

    /// <summary>The name as the site writes it in a player link: "Zoë & Co!" is "zoe-and-co".</summary>
    public static string PlayerSlug(string name)
    {
        var plain = new string(
            name.Normalize(System.Text.NormalizationForm.FormKD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()
        );
        var slug = Regex.Replace(plain.ToLowerInvariant().Trim().Replace("&", " and "), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "player" : slug;
    }

    // The matching ']' of the array that starts at `start`, skipping brackets inside strings.
    private static int ArrayEnd(string text, int start)
    {
        var depth = 0;
        var quoted = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '\\')
                    i++;
                else if (c == '"')
                    quoted = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case '['
                or '{':
                    depth++;
                    break;
                case ']'
                or '}':
                    if (--depth == 0)
                        return i;
                    break;
            }
        }

        return -1;
    }

    [GeneratedRegex("""season=([a-z0-9-]+)\\?",[^{}]*?\\?"children\\?":\\?"([^"\\]+)""")]
    private static partial Regex SeasonLinkRegex();

    [GeneratedRegex("""seasonParam\\?":\\?"([a-z0-9-]+)""")]
    private static partial Regex SeasonParamRegex();

    /// <summary>The season the page shows (the current one when none was asked for).</summary>
    public static string? ParseSeason(string? html) =>
        html is null ? null
        : SeasonParamRegex().Match(html) is { Success: true } match ? match.Groups[1].Value
        : null;

    /// <summary>The seasons in the page's season picker, newest first as the site lists them.</summary>
    public static List<RrLoungeSeasonLink> ParseSeasons(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return [];
        return SeasonLinkRegex()
            .Matches(html)
            .Select(match => new RrLoungeSeasonLink(match.Groups[1].Value, match.Groups[2].Value))
            .DistinctBy(season => season.Slug)
            .ToList();
    }

    /// <summary>
    /// Loads a ladder: the page gives every player's stats, the ranks and the seasons. Should the page change shape, the
    /// API still gives rank, name and MMR. Null when neither can be reached.
    /// </summary>
    public static async Task<RrLoungeLadderData?> LoadAsync(IApiCaller<IRrLoungeApi> api, RrLoungeLadder ladder, string? season)
    {
        var category = Query(ladder);
        var page = await api.CallApiAsync(lounge => lounge.GetLeaderboardPageAsync(category, season));
        var html = page.IsSuccess ? page.Value : null;
        var players = ParseStats(html);
        if (players.Count == 0)
        {
            var board = await api.CallApiAsync(lounge => lounge.GetLeaderboardAsync(category, season));
            if (board.IsFailure)
                return page.IsSuccess ? new([], ParseTiers(html), ParseSeasons(html), season) : null;
            players = board.Value?.Players ?? [];
        }

        return new(Ranked(players), ParseTiers(html), ParseSeasons(html), season ?? ParseSeason(html));
    }

    /// <summary>
    /// What changed between two loads of a ladder: each player whose MMR moved, with how much, and each new player
    /// (null). Players with the same MMR are left out.
    /// </summary>
    public static Dictionary<string, int?> Changes(IEnumerable<RrLoungePlayer> before, IEnumerable<RrLoungePlayer> after)
    {
        var old = before
            .Where(player => player.Id.Length > 0)
            .DistinctBy(player => player.Id)
            .ToDictionary(player => player.Id, player => player.Mmr);
        var changes = new Dictionary<string, int?>();
        foreach (var player in after.Where(player => player.Id.Length > 0))
        {
            if (!old.TryGetValue(player.Id, out var mmr))
                changes[player.Id] = null;
            else if (mmr != player.Mmr)
                changes[player.Id] = player.Mmr - mmr;
        }

        return changes;
    }

    /// <summary>"Season 7" for "Retro Rewind Season 7": every season is Retro Rewind's, and the launcher is narrow.</summary>
    public static string ShortSeasonName(string name)
    {
        const string prefix = "Retro Rewind ";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length ? name[prefix.Length..] : name;
    }

    /// <summary>Players whose name contains the text, ignoring case; everyone for an empty search.</summary>
    public static IEnumerable<RrLoungePlayer> Search(IEnumerable<RrLoungePlayer> players, string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? players
            : players.Where(player => player.Name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The 16×12 flag picture the site shows next to a name, or null without a country.</summary>
    public static string? FlagUrl(string? countryCode) =>
        countryCode is { Length: 2 } code && code.All(char.IsAsciiLetter)
            ? $"https://flagcdn.com/16x12/{code.ToLowerInvariant()}.png"
            : null;

    /// <summary>The full address of a rank's badge, or null when it has none (only the site's own pictures are used).</summary>
    public static string? IconUrl(RrLoungeTier? tier) =>
        tier?.Icon is { Length: > 1 } icon
        && icon.StartsWith("/icons/", StringComparison.Ordinal)
        && !icon.Contains("..", StringComparison.Ordinal)
            ? SiteUrl + icon
            : null;

    /// <summary>"94.1", or "-" when the stat is unknown, like the site.</summary>
    public static string Number(double? value, int decimals = 1) =>
        value is { } number ? number.ToString("F" + decimals, CultureInfo.InvariantCulture) : "-";
}

public static class RrLoungeExtensions
{
    public static IServiceCollection AddRrLounge(this IServiceCollection services)
    {
        services.AddWhWzRefitApi<IRrLoungeApi>(
            RrLoungeData.SiteUrl,
            new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
        services.AddSingleton<RrLoungeImages>();
        return services;
    }
}
