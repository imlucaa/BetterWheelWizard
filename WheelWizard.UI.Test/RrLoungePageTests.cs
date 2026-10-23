using System.Linq.Expressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NSubstitute;
using WheelWizard.RrLounge;
using WheelWizard.RrLounge.Views;
using WheelWizard.Shared;
using WheelWizard.Shared.Services;
using WheelWizard.Views.Components;

namespace WheelWizard.UI.Test;

public class RrLoungePageTests
{
    private sealed class DirectCaller(IRrLoungeApi api) : IApiCaller<IRrLoungeApi>
    {
        public async Task<OperationResult<TResult>> CallApiAsync<TResult>(Expression<Func<IRrLoungeApi, Task<TResult>>> apiCall) =>
            await OperationResult.TryCatch(async () => await apiCall.Compile()(api));
    }

    // 60 players, so the table has two pages; Ann has the most MMR.
    private static string Page()
    {
        var players = Enumerable
            .Range(1, 60)
            .Select(i =>
                $$"""{\"id\":\"p{{i}}\",\"name\":\"{{(i == 60 ? "Ann" : $"Player{i}")}}\",\"countryCode\":null,\"mmr\":{{i * 100}},\"peakMmr\":{{i * 100}},\"mogiCount\":{{i}},\"winPercentage\":50,\"last10Wins\":5,\"last10Losses\":5,\"averageScore\":80,\"averageScoreLast10\":81,\"partnerAverageScore\":70}"""
            );
        return $$"""
            [\"$\",\"$L2\",\"a\",{\"href\":\"/leaderboard?category=rr&season=s7\",\"children\":\"Season 7\"}],[\"$\",\"$L2\",\"b\",{\"href\":\"/leaderboard?category=rr&season=s6\",\"children\":\"Season 6\"}],
            {\"players\":[{{string.Join(
                ",",
                players
            )}}],\"rankTiers\":[{\"name\":\"Gold\",\"minMmr\":0,\"color\":\"#eeca00\"}],\"seasonParam\":\"s7\"}
            """;
    }

    // A cut-down copy of a player page on rr-lounge.com, which is plain HTML.
    private const string ProfilePage =
        @"<html><body><div><img src=""/icons/new_diamond.png"" alt=""Diamond"" width=""100"" height=""100""/><p style=""color:#b0f442"">DIVISION: <!-- -->DIAMOND</p></div>
<div class=""flex items-center gap-2 text-3xl""><img src=""https://flagcdn.com/16x12/us.png"" width=""16"" height=""12"" alt=""US"" class=""inline-block""/>owen &amp; co</div><p class=""text-base text-zinc-400"">Retro Rewind CT Season 2</p>
<p><span style=""color:#4286f4"">Rank</span>: <!-- -->1<!-- --> / <!-- -->129</p><p><span style=""color:#4286f4"">Percentile</span>: <!-- -->99<!-- -->th</p>
<p><span style=""color:#4286f4"">Peak</span>: <!-- -->6590<!-- --> <span style=""color:#4286f4"">Lowest</span>: <!-- -->2250</p>
<table><tbody><tr style=""background-color:#222""><td>Wins</td><td>Losses</td><td>Win %</td></tr><tr><td><span>28</span></td><td><span>10</span></td><td>73.7</td></tr></tbody></table>
<table><tbody><tr><td>Streak</td><td>W - L (10)</td></tr><tr><td><span>-2</span></td><td>7-3</td></tr></tbody></table>
<table><thead><tr><td>#</td><td>Date</td><td>Tier</td><td>Format</td><td>Races</td><td>Rank</td><td>Score</td><td>Result</td><td>+/-</td><td>MMR</td></tr></thead><tbody>
<tr style=""background-color:#4c7a01""><td>3</td><td><a href=""/mogis/x"">Oct 3, 2026, 9:17 PM</a></td><td>Tier Squad Queue</td><td>2v2</td><td>12</td><td>1st</td><td>119</td><td>Win</td><td>+163</td><td>6487</td></tr>
<tr style=""background-color:#3b3b3b""><td>2</td><td>Aug 22, 2026, 12:26 AM</td><td>-</td><td>Adjustment</td><td>-</td><td>-</td><td>-</td><td>-</td><td>+40</td><td>6324</td></tr>
<tr style=""background-color:#75291b""><td>1</td><td>Aug 9, 2026, 5:02 AM</td><td>Tier All</td><td>2v2</td><td>12</td><td>5th</td><td>70</td><td>Loss</td><td>-266</td><td>6284</td></tr>
</tbody></table></body></html>";

    private static void Settle()
    {
        for (var i = 0; i < 10; i++)
            Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Texts(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? "").ToList();

    [AvaloniaFact]
    public void ShowsTheLadderLikeTheSite_WithSeasonsPagesSearchAndSorting()
    {
        var api = Substitute.For<IRrLoungeApi>();
        api.GetLeaderboardPageAsync("rr", null).Returns(Page());
        api.GetLeaderboardPageAsync("rr", "s6").Returns(Page().Replace("Ann", "Bo"));

        var page = new RrLoungePage(new DirectCaller(api));
        var window = new Window
        {
            Content = page,
            Width = 1100,
            Height = 800,
        };
        window.Show();
        Settle();

        var texts = Texts(window);
        Assert.Contains("RR Lounge", texts);
        Assert.Contains(texts, text => text.StartsWith("1-50 of 60 players"));
        Assert.Contains("1 / 2", texts);
        Assert.Contains("Ann", texts);
        Assert.DoesNotContain("Player1", texts);
        Assert.Contains("6000", texts);

        // The seasons the site lists, current one picked; another one loads its own table.
        var seasons = window.GetVisualDescendants().OfType<SegmentedControl>().Single(control => control.ItemCount == 2);
        Assert.Equal("s7", (seasons.SelectedItem as SegmentOption)?.Tag);
        seasons.SelectedIndex = 1;
        Settle();
        Assert.Contains("Bo", Texts(window));

        // Page two has the rest.
        window
            .GetVisualDescendants()
            .OfType<ActionButton>()
            .First(button => button.Text == "Next")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle();
        texts = Texts(window);
        Assert.Contains(texts, text => text.StartsWith("51-60 of 60 players"));
        Assert.Contains("Player1", texts);

        // Search.
        window.GetVisualDescendants().OfType<InputField>().Single().Text = "player59";
        Settle();
        texts = Texts(window);
        Assert.Contains(texts, text => text.StartsWith("1-1 of 1 players (60 total)"));
        Assert.Contains("Player59", texts);

        // Sorting by MMR again flips it, lowest first.
        window.GetVisualDescendants().OfType<InputField>().Single().Text = "";
        var mmr = window.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "MMR");
        var point = mmr.TranslatePoint(new Point(2, mmr.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
        texts = Texts(window);
        Assert.Contains("Player1", texts);
        Assert.DoesNotContain("Bo", texts);

        // Clicking a player opens their profile: division, rank, stats, MMR trend and mogis.
        api.GetPlayerPageAsync("player1", "rr", Arg.Any<string?>()).Returns(ProfilePage);
        var name = window.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Player1");
        point = name.TranslatePoint(new Point(2, name.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
        Assert.Equal("Player1", page.OpenProfileName);
        texts = Texts(window);
        Assert.Contains("owen & co", texts);
        Assert.Contains("Diamond", texts);
        Assert.Contains("Rank 1 / 129", texts);
        Assert.Contains("99th percentile", texts);
        Assert.Contains("MMR trend", texts);
        Assert.Contains("3 mogis", texts);
        Assert.Contains("Oct 3, 9:17 PM", texts);
        Assert.Contains("+163", texts);
        Assert.Contains("Adj.", texts);
        Assert.Single(window.GetVisualDescendants().OfType<MmrTrendChart>());

        // Back returns to the table as it was.
        window
            .GetVisualDescendants()
            .OfType<ActionButton>()
            .Single(button => button.Text == "Back")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle();
        Assert.Null(page.OpenProfileName);
        Assert.Contains("Player1", Texts(window));
        window.Close();
    }

    [AvaloniaFact]
    public void WhenTheSiteCannotBeReached_ItSaysSo_AndRetryWorks()
    {
        var api = Substitute.For<IRrLoungeApi>();
        var online = false;
        api.GetLeaderboardPageAsync("rr", null).Returns(_ => online ? "<html></html>" : throw new HttpRequestException("offline"));
        api.GetLeaderboardAsync("rr", null)
            .Returns(_ =>
                online
                    ? new RrLoungeBoard
                    {
                        Players =
                        [
                            new()
                            {
                                Id = "a",
                                Name = "Ann",
                                Mmr = 900,
                            },
                        ],
                    }
                    : throw new HttpRequestException("offline")
            );

        var page = new RrLoungePage(new DirectCaller(api));
        var window = new Window
        {
            Content = page,
            Width = 1100,
            Height = 800,
        };
        window.Show();
        Settle();
        Assert.Contains("Could not reach rr-lounge.com", Texts(window));

        // Back online, but the page changed shape: the API still gives rank, name and MMR.
        online = true;
        window
            .GetVisualDescendants()
            .OfType<ActionButton>()
            .Single(button => button.Text == "Retry")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle();
        var texts = Texts(window);
        Assert.Contains("Ann", texts);
        Assert.Contains("900", texts);
        window.Close();
    }

    [AvaloniaFact]
    public void ItUpdatesLive_KeepingThePageAndSearch_AndKeepsTheOldTableWhenOffline()
    {
        var api = Substitute.For<IRrLoungeApi>();
        var html = Page();
        var online = true;
        api.GetLeaderboardPageAsync("rr", Arg.Any<string?>()).Returns(_ => online ? html : throw new HttpRequestException("offline"));
        api.GetLeaderboardAsync("rr", Arg.Any<string?>()).Returns<RrLoungeBoard>(_ => throw new HttpRequestException("offline"));

        var page = new RrLoungePage(new DirectCaller(api));
        var window = new Window
        {
            Content = page,
            Width = 1100,
            Height = 800,
        };
        window.Show();
        Settle();
        window.GetVisualDescendants().OfType<InputField>().Single().Text = "ann";
        Settle();
        Assert.Contains("6000", Texts(window));

        // Ann plays a mogi: the next refresh shows her new MMR, still searched.
        html = html.Replace("\\\"mmr\\\":6000", "\\\"mmr\\\":6123");
        page.RefreshAsync().GetAwaiter().GetResult();
        Settle();
        var texts = Texts(window);
        Assert.Contains("6123", texts);
        // Ann just played, so her row is marked until the next refresh.
        var annRow = window
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .First(text => text.Text == "Ann")
            .GetVisualAncestors()
            .OfType<Border>()
            .First(border => ToolTip.GetTip(border) is string);
        Assert.Equal("+123 MMR in the last update · open profile", ToolTip.GetTip(annRow));
        Assert.Contains(texts, text => text.StartsWith("1-1 of 1 players (60 total)"));

        // Offline: the last table stays, no error screen.
        online = false;
        page.RefreshAsync().GetAwaiter().GetResult();
        Settle();
        texts = Texts(window);
        Assert.Contains("6123", texts);
        Assert.DoesNotContain("Could not reach rr-lounge.com", texts);
        window.Close();
    }

    [Fact]
    public void DarkRankColours_AreMadeReadable_KeepingTheirHue()
    {
        var legend = RrLoungePage.Readable(Avalonia.Media.Color.Parse("#470000"));
        Assert.True(legend.R > 200 && legend.G < 20 && legend.B < 20);
        Assert.Equal(Avalonia.Media.Color.Parse("#b9f2ff"), RrLoungePage.Readable(Avalonia.Media.Color.Parse("#b9f2ff")));
    }

    [AvaloniaFact]
    public void ItFollowsANewSeason_AndShowsNewPlayers_WhenNoSeasonWasPicked()
    {
        var api = Substitute.For<IRrLoungeApi>();
        var html = Page();
        api.GetLeaderboardPageAsync("rr", null).Returns(_ => html);

        var page = new RrLoungePage(new DirectCaller(api));
        var window = new Window
        {
            Content = page,
            Width = 1100,
            Height = 800,
        };
        window.Show();
        Settle();
        Assert.Contains(Texts(window), text => text.StartsWith("1-50 of 60 players"));

        // Season 8 starts and someone joins: the refresh picks both up without touching the tabs.
        html = html.Replace("seasonParam\\\":\\\"s7", "seasonParam\\\":\\\"s8")
            .Replace(
                "{\\\"href\\\":\\\"/leaderboard?category=rr\u0026season=s7\\\",\\\"children\\\":\\\"Season 7\\\"}",
                "{\\\"href\\\":\\\"/leaderboard?category=rr\u0026season=s8\\\",\\\"children\\\":\\\"Season 8\\\"}],[{\\\"href\\\":\\\"/leaderboard?category=rr\u0026season=s7\\\",\\\"children\\\":\\\"Season 7\\\"}"
            )
            .Replace("\\\"players\\\":[", "\\\"players\\\":[{\\\"id\\\":\\\"new\\\",\\\"name\\\":\\\"Newbie\\\",\\\"mmr\\\":50},");
        page.RefreshAsync().GetAwaiter().GetResult();
        Settle();

        var texts = Texts(window);
        Assert.Contains(texts, text => text.StartsWith("1-50 of 61 players"));
        var seasons = window
            .GetVisualDescendants()
            .OfType<SegmentedControl>()
            .Single(control => control.Items.OfType<SegmentOption>().Any(option => option.Tag is string));
        Assert.Equal(3, seasons.ItemCount);
        Assert.Equal("s8", (seasons.SelectedItem as SegmentOption)?.Tag);
        window.Close();
    }
}
