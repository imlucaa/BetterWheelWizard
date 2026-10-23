using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using WheelWizard.Views.Components;
using WheelWizard.Views.Shell;

namespace WheelWizard.RrLounge.Views;

/// <summary>
/// A player's profile, like their page on rr-lounge.com: division, rank, the season's numbers, the MMR trend and every
/// mogi. It opens over the table, refreshes with it, and Back (or the mouse's back button) returns to the table as it was.
/// </summary>
public sealed partial class RrLoungePage
{
    // #, date, format, place, score, +/-, MMR.
    private const string MogiColumns = "26,*,34,32,36,40,44";

    private readonly ContentControl _profileView = new() { IsVisible = false };
    private string? _profileName;
    private RrLoungeProfile? _profile;
    private int _profileVersion;
    private bool _profileLoading;
    private ScrollViewer? _profileScroll;

    // The site's labels are long for the narrow launcher; the full one shows on hover.
    private static readonly Dictionary<string, string> ShortLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Avg Score"] = "Avg",
        ["W - L (10)"] = "L10",
        ["Avg (10)"] = "Avg L10",
        ["Partner Avg"] = "Partner",
        ["Partner Avg (10)"] = "Partner L10",
    };

    /// <summary>The name of the player whose profile is open, or null on the table.</summary>
    public string? OpenProfileName => _profileName;

    private void InitProfile() =>
        AddHandler(
            PointerReleasedEvent,
            (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.XButton1 && _profileName is not null)
                {
                    CloseProfile();
                    e.Handled = true;
                }
            },
            handledEventsToo: true
        );

    public void OpenProfile(string name)
    {
        _profileName = name;
        _profile = null;
        UpdateViews();
        _ = LoadProfileAsync();
    }

    public void CloseProfile()
    {
        _profileName = null;
        _profile = null;
        _profileScroll = null;
        _profileView.Content = null;
        _profileVersion++;
        UpdateViews();
    }

    private async Task LoadProfileAsync(bool quiet = false)
    {
        if (_profileName is not { } name || (quiet && (_profileLoading || _profile is null)))
            return;
        var version = ++_profileVersion;
        _profileLoading = true;
        if (!quiet)
            _profileView.Content = ProfileFrame(name, Loading());

        RrLoungeProfile? profile;
        try
        {
            profile = await RrLoungeProfiles.LoadAsync(_api, name, _ladder, _season);
        }
        finally
        {
            if (version == _profileVersion)
                _profileLoading = false;
        }

        if (version != _profileVersion || _profileName != name)
            return;
        if (profile is null)
        {
            if (!quiet)
                _profileView.Content = ProfileFrame(name, ProfileProblem(name));
            return;
        }

        var offset = _profileScroll?.Offset ?? default;
        _profile = profile;
        _profileView.Content = ProfileFrame(name, ProfileBody(profile));
        if (quiet && _profileScroll is { } scroll)
            Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);
    }

    /// <summary>The live refresh of the open profile, done now.</summary>
    public Task RefreshProfileAsync() => LoadProfileAsync(quiet: true);

    // Back and the site link above whatever the profile shows (loading, a problem, or the profile).
    private Control ProfileFrame(string name, Control content)
    {
        var back = new ActionButton
        {
            Text = "Back",
            IconData = Application.Current?.TryFindResource("ArrowLeft", out var arrow) == true ? arrow as Geometry : null,
            Size = ButtonSize.Compact,
            Tone = ActionButtonTone.Secondary,
        };
        back.Click += (_, _) => CloseProfile();
        var site = new ActionButton
        {
            Text = "rr-lounge.com",
            IconData = Application.Current?.TryFindResource("ExternalLink", out var link) == true ? link as Geometry : null,
            Size = ButtonSize.Compact,
            Tone = ActionButtonTone.Secondary,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(site, "Open this profile on rr-lounge.com");
        site.Click += (_, _) => ViewUtils.OpenLink(RrLoungeData.PlayerUrl(name, _ladder, _season));
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 0, 10),
            Children = { back, site },
        };
        Grid.SetColumn(site, 1);
        DockPanel.SetDock(bar, Dock.Top);
        return new DockPanel { Children = { bar, content } };
    }

    private Control ProfileProblem(string name)
    {
        var retry = new ActionButton
        {
            Text = "Retry",
            Tone = ActionButtonTone.Brand,
            Width = 150,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        retry.Click += (_, _) => _ = LoadProfileAsync();
        var heading = Cell($"Could not load {name}", "Neutral50");
        heading.FontSize = 17;
        return new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { heading, Cell("Check your connection and try again.", "Neutral400"), retry },
        };
    }

    private static Border Card(Control child, Thickness? padding = null)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = padding ?? new Thickness(12),
            Child = child,
        };
        Themed(card, Border.BackgroundProperty, "Neutral900");
        Themed(card, Border.BorderBrushProperty, "Neutral800");
        return card;
    }

    private RrLoungeTier? TierNamed(string name) =>
        (_data?.Tiers ?? []).FirstOrDefault(tier => tier.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private Control ProfileBody(RrLoungeProfile profile)
    {
        var tiers = _data?.Tiers ?? [];
        var tier = TierNamed(profile.Division) ?? RrLoungeData.TierFor(profile.Mmr, tiers);

        // Who: flag and name, the season, then division and rank.
        var name = Cell(profile.Name.Length > 0 ? profile.Name : _profileName ?? "", "Neutral50");
        name.FontSize = 22;
        name.FontWeight = FontWeight.Bold;
        var who = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (Flag(profile.CountryCode) is { } flag)
            who.Children.Add(flag);
        who.Children.Add(name);
        var season = Cell(profile.Season, "Neutral400");
        season.FontSize = 12;

        var standing = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        void Add(Control part)
        {
            if (standing.Children.Count > 0)
                standing.Children.Add(Spacer());
            standing.Children.Add(part);
        }

        if (profile.Division.Length > 0)
        {
            var division = tier is null ? Cell(profile.Division) : Colored(profile.Division, tier.Color);
            division.FontWeight = FontWeight.SemiBold;
            Add(division);
        }

        if (profile.Rank is { } rank)
            Add(Cell(profile.Players is { } players ? $"Rank {rank} / {players}" : $"Rank {rank}"));
        if (profile.Percentile is { } percentile)
            Add(Cell($"{percentile} percentile", "Neutral300"));

        var range = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (profile.Mmr is { } now)
            range.Children.Add(Pair("MMR", Mmr(now)));
        if (profile.Peak is { } peak)
            range.Children.Add(Pair("Peak", Mmr(peak)));
        if (profile.Lowest is { } lowest)
            range.Children.Add(Pair("Lowest", Mmr(lowest)));

        var top = new StackPanel { Spacing = 6 };
        if (RankIcon(tier, 72) is { } badge)
        {
            badge.HorizontalAlignment = HorizontalAlignment.Center;
            top.Children.Add(badge);
        }

        top.Children.Add(who);
        top.Children.Add(season);
        top.Children.Add(standing);
        top.Children.Add(range);
        var header = Card(top);

        // The season's numbers, in the site's order.
        var stats = new UniformGrid { Columns = 5 };
        foreach (var (label, value) in profile.Stats)
        {
            var title = Cell(ShortLabels.GetValueOrDefault(label, label), "Neutral400");
            title.FontSize = 11;
            var number = label switch
            {
                "Wins" => Colored(value, "#B0F442"),
                "Losses" => Colored(value, "#E55A2B"),
                "Streak" => int.TryParse(value, out var streak) && streak < 0 ? Colored(value, "#E55A2B") : Colored(value, "#B0F442"),
                "Win %" => Colored(value, "#B0F442"),
                _ => Cell(value, "Neutral50"),
            };
            number.FontSize = 15;
            number.FontWeight = FontWeight.SemiBold;
            var stat = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(0, 4),
                Children = { title, number },
            };
            ToolTip.SetTip(stat, label);
            stats.Children.Add(stat);
        }

        var body = new StackPanel { Spacing = 10, Children = { header } };
        if (stats.Children.Count > 0)
            body.Children.Add(Card(stats, new Thickness(6, 6)));

        // The MMR trend, oldest first, from the MMR before the first mogi.
        var history = profile.History;
        var points = history.Where(mogi => mogi.Mmr is not null).Reverse().Select(mogi => mogi.Mmr!.Value).ToList();
        if (history.Count > 0 && history[^1] is { Mmr: { } firstMmr } first && int.TryParse(first.Change, out var change))
            points.Insert(0, firstMmr - change);
        if (points.Count > 1)
        {
            var heading = Cell("MMR trend", "Neutral200", HorizontalAlignment.Left);
            heading.FontWeight = FontWeight.SemiBold;
            body.Children.Add(
                Card(
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            heading,
                            new MmrTrendChart
                            {
                                Height = 170,
                                Points = points,
                                Tiers = tiers,
                                FirstDate = ShortDate(history[^1].Date),
                                LastDate = ShortDate(history[0].Date),
                            },
                        },
                    }
                )
            );
        }

        body.Children.Add(MogiTable(history));

        _profileScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body,
        };
        return _profileScroll;
    }

    private static TextBlock Spacer()
    {
        var dot = Cell("  ·  ", "Neutral500");
        dot.TextTrimming = TextTrimming.None;
        return dot;
    }

    private static Control Pair(string label, Control value)
    {
        var title = Cell(label, "Neutral400");
        title.FontSize = 12;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(6, 0),
            Children = { title, value },
        };
    }

    /// <summary>"Oct 3, 9:17 PM" for "Oct 3, 2026, 9:17 PM": the season is within the year anyway.</summary>
    public static string ShortDate(string date) => Regex.Replace(date, @",\s*\d{4}", "");

    private Control MogiTable(List<RrLoungeMogi> history)
    {
        var heading = Cell(history.Count == 1 ? "1 mogi" : $"{history.Count} mogis", "Neutral200", HorizontalAlignment.Left);
        heading.FontWeight = FontWeight.SemiBold;
        heading.Margin = new Thickness(12, 10, 12, 8);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(MogiColumns),
            ColumnSpacing = 4,
            Margin = new Thickness(8, 0, 8, 6),
        };
        string[] titles = ["#", "Date", "Size", "Place", "Score", "+/-", "MMR"];
        for (var i = 0; i < titles.Length; i++)
        {
            var title = Cell(titles[i], "Neutral400", i == 1 ? HorizontalAlignment.Left : HorizontalAlignment.Center);
            title.FontSize = 11;
            Grid.SetColumn(title, i);
            header.Children.Add(title);
        }

        var list = new StackPanel { Children = { heading, header } };
        if (history.Count == 0)
        {
            var none = Cell("No mogis this season yet.", "Neutral400");
            none.Margin = new Thickness(0, 6, 0, 14);
            list.Children.Add(none);
        }

        foreach (var mogi in history)
            list.Children.Add(MogiRow(mogi));
        return Card(list, new Thickness(0, 0, 0, 6));
    }

    // A win is green and a loss red, like the site, but softer so the numbers stay easy to read; an adjustment is grey.
    private static Control MogiRow(RrLoungeMogi mogi)
    {
        Control[] cells =
        [
            Cell(mogi.Number.ToString(), "Neutral300"),
            Cell(ShortDate(mogi.Date), "Neutral100", HorizontalAlignment.Left),
            Cell(mogi.IsAdjustment ? "Adj." : mogi.Format),
            Cell(mogi.Place),
            Cell(mogi.Score),
            mogi.Change.StartsWith('-') ? Colored(mogi.Change, "#FF8A6B") : Colored(mogi.Change, "#B0F442"),
            Cell(mogi.Mmr?.ToString() ?? "-", "Neutral50"),
        ];
        var line = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(MogiColumns),
            ColumnSpacing = 4,
            MinHeight = 30,
        };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is TextBlock text)
                text.FontSize = 12;
            Grid.SetColumn(cells[i], i);
            line.Children.Add(cells[i]);
        }

        var tint =
            mogi.IsWin ? Color.FromArgb(70, 0x4C, 0x7A, 0x01)
            : mogi.IsLoss ? Color.FromArgb(80, 0x75, 0x29, 0x1B)
            : Color.FromArgb(60, 0x3B, 0x3B, 0x3B);
        var row = new Border
        {
            Background = new SolidColorBrush(tint),
            Padding = new Thickness(8, 0),
            Margin = new Thickness(6, 1),
            CornerRadius = new CornerRadius(4),
            Child = line,
        };
        ToolTip.SetTip(
            row,
            mogi.IsAdjustment
                ? $"MMR adjustment, {mogi.Date}"
                : $"{mogi.Tier} · {mogi.Format} · {mogi.Races} races · {mogi.Result} · {mogi.Date}"
        );
        return row;
    }
}
