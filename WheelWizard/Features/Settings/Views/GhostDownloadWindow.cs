using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WheelWizard.Ghosts;
using WheelWizard.RrRooms;
using WheelWizard.Shared.Services;
using WheelWizard.Views.Components;

namespace WheelWizard.Settings.Views;

/// <summary>
/// Downloads ghosts from RWFC's time trial board (rwfc.net/tt), laid out like the website: a "Browse tracks" panel with
/// the site's filters, then every track with its world record. Clicking a track opens its full board; clicking a time
/// there puts that ghost on the track. The ghost it replaces is kept in the Replaced folder, like an import.
/// </summary>
public sealed class GhostDownloadWindow : Window
{
    private readonly GhostImporter _importer;
    private readonly IApiCaller<IRwfcApi> _api;
    private readonly Action _installed;
    private readonly Control _owner;

    private readonly ContentControl _body = new();
    private readonly TextBlock _status;

    // Every board track that has a ghost slot in this Retro Rewind, by board id.
    private readonly Dictionary<int, (GhostTrack Slot, RwfcTimeTrialTrack Board)> _tracks = [];

    // The filters, the same as on the website.
    private bool _custom;
    private TimeTrialFilter _filter = new();
    private bool _noGlitch;
    private string _search = string.Empty;
    private bool _ghostsOnly;
    private int _lastBrowse;
    private int _rankCategory;
    private List<RwfcTrackRecord> _records = [];
    private int _recordsVersion;

    // The board of one track.
    private (GhostTrack Slot, RwfcTimeTrialTrack Board)? _track;
    private readonly List<RwfcTimeTrialTime> _times = [];
    private int _page;
    private int _totalPages;
    private int _boardVersion;
    private bool _installing;

    // Column widths of the tables, shared by the header and the rows so they line up.
    private const string RecordColumns = "2.3*,1.1*,1.9*,1.2*,1.5*,1.6*,1*,0.6*";
    private const string RankingColumns = "0.5*,4*,0.7*";
    private const string BoardColumns = "0.5*,1.1*,1.9*,1.2*,1.5*,1.6*,1*,Auto";

    // The time whose ghost was put on a track in this window, per track and speed, so its row can say "In use".
    private readonly Dictionary<(int Board, int Cc), int> _inUse = [];

    // The track to open straight away, when the window was opened from one.
    private readonly GhostTrack? _start;

    public GhostDownloadWindow(
        GhostImporter importer,
        IApiCaller<IRwfcApi> api,
        Control owner,
        Action installed,
        GhostTrack? start = null,
        TimeTrialFilter? filter = null,
        bool noGlitch = false,
        bool custom = false
    )
    {
        _start = start;
        // Opened from the Ghosts page, it starts with the same filters.
        _filter = filter ?? new();
        _noGlitch = noGlitch;
        _custom = custom;
        _importer = importer;
        _api = api;
        _owner = owner;
        _installed = installed;

        Title = "Time trial ghosts";
        Width = 1040;
        Height = 820;
        MinWidth = 760;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // Solid like the other pop-ups: the default window style is see-through.
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        Background = Solid("Neutral950", Colors.Black);

        _status = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 10, 0, 0),
            Foreground = Brush("Neutral300", Brushes.Gray),
        };
        // Like the website: the title on the left, and a switch between the tracks and the player rankings.
        var views = Segmented(
            ["Tracks", "Player rankings"],
            0,
            i =>
            {
                if (i == 1)
                    ShowRankings();
                else
                    ShowBrowse();
            }
        );
        views.Width = 300;
        views.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(views, 1);
        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 0, 0, 14),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        Text("Time trial leaderboards", 22, "Neutral50", FontWeight.Bold),
                        Text("World records for every Retro Rewind track, from rwfc.net/tt", 12, "Neutral400"),
                    },
                },
                views,
            },
        };
        Grid.SetRow(_body, 1);
        Grid.SetRow(_status, 2);
        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(22, 18),
            Children = { top, _body, _status },
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        Opened += async (_, _) => await LoadTracksAsync();
    }

    #region Colours and building blocks

    private IBrush Brush(string key, IBrush fallback) =>
        _owner.TryFindResource(key, _owner.ActualThemeVariant, out var found)
            ? found switch
            {
                IBrush brush => brush,
                Color color => new SolidColorBrush(color),
                _ => fallback,
            }
            : fallback;

    private IBrush Solid(string key, Color fallback) =>
        Brush(key, new SolidColorBrush(fallback)) is SolidColorBrush solid
            ? new SolidColorBrush(Color.FromRgb(solid.Color.R, solid.Color.G, solid.Color.B))
            : new SolidColorBrush(fallback);

    private IBrush Accent => Brush("Primary300", Brushes.DodgerBlue);

    private TextBlock Text(string text, double size = 13, string brush = "Neutral100", FontWeight? weight = null) =>
        new()
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(brush, Brushes.White),
        };

    // A main line with a smaller grey line under it (for example a vehicle and its drift).
    private StackPanel TwoLines(string top, string? bottom, string topBrush = "Neutral100", FontWeight? weight = null)
    {
        var panel = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Text(top, 13, topBrush, weight));
        if (!string.IsNullOrWhiteSpace(bottom))
            panel.Children.Add(Text(bottom!, 11, "Neutral400"));
        return panel;
    }

    private void Say(string text) => _status.Text = text;

    private Control Message(string text) =>
        new TextBlock
        {
            Text = text,
            FontSize = 13,
            Margin = new Thickness(16, 18),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("Neutral400", Brushes.Gray),
        };

    /// <summary>A row of options like the website's: the picked one is filled with the accent colour.</summary>
    private Border Segmented(string[] options, int selected, Action<int> picked)
    {
        var cells = new UniformGrid { Rows = 1, Columns = options.Length };
        var buttons = new List<Border>();

        void Paint(int chosen)
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].Background = i == chosen ? Accent : Brushes.Transparent;
                ((TextBlock)buttons[i].Child!).Foreground =
                    i == chosen ? Brush("LauncherAccentTextBrush", Brushes.White) : Brush("Neutral300", Brushes.Gray);
            }
        }

        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var button = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 7),
                Margin = new Thickness(2),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock
                {
                    Text = options[i],
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            };
            button.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left)
                    return;
                Paint(index);
                picked(index);
            };
            buttons.Add(button);
            cells.Children.Add(button);
        }

        Paint(selected);
        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("Neutral700", Brushes.Gray),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(2),
            Child = cells,
        };
    }

    private StackPanel Labelled(string label, Control control) =>
        new()
        {
            Spacing = 5,
            Margin = new Thickness(6),
            Children = { Text(label, 12, "Neutral200", FontWeight.SemiBold), control },
        };

    private Grid HeaderRow(string columns, params string[] titles)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            ColumnSpacing = 12,
            Margin = new Thickness(16, 10),
        };
        for (var i = 0; i < titles.Length; i++)
        {
            var title = Text(titles[i].ToUpperInvariant(), 11, "Neutral300", FontWeight.Bold);
            Grid.SetColumn(title, i);
            grid.Children.Add(title);
        }

        return grid;
    }

    /// <summary>A table row that lights up on hover; <paramref name="click"/> null makes it a plain row.</summary>
    private Border TableRow(string columns, Control?[] cells, Action? click)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 12 };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is not { } cell)
                continue;
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        var row = new Border
        {
            Padding = new Thickness(16, 10),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Brush("Neutral800", Brushes.Gray),
            Background = Brushes.Transparent,
            MinHeight = 58,
            Child = grid,
        };
        if (click is null)
            return row;

        row.Cursor = new Cursor(StandardCursorType.Hand);
        row.PointerEntered += (_, _) => row.Background = Brush("Neutral900", Brushes.Transparent);
        row.PointerExited += (_, _) => row.Background = Brushes.Transparent;
        row.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
                click();
        };
        return row;
    }

    // The holder column: name, Mii name under it, country code next to it.
    private Control Holder(RwfcTimeTrialTime time)
    {
        var lines = TwoLines(time.PlayerName, time.MiiName, "Neutral50", FontWeight.SemiBold);
        // The name opens the player's profile (and not the row's own action).
        if (time.TtProfileId > 0)
        {
            lines.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(lines, $"See {time.PlayerName}'s times");
            var profile = time.TtProfileId;
            lines.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left)
                    return;
                e.Handled = true;
                ShowProfile(profile);
            };
        }

        if (string.IsNullOrWhiteSpace(time.CountryAlpha2))
            return lines;
        var country = Text(time.CountryAlpha2!, 10, "Neutral300", FontWeight.Bold);
        var badge = new Border
        {
            Background = Brush("Neutral800", Brushes.Gray),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = country,
        };
        Grid.SetColumn(badge, 1);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            Children = { lines, badge },
        };
    }

    private Control?[] RunCells(RwfcTimeTrialTime time) =>
        [
            Holder(time),
            Text(TimeTrials.CharacterName(time.CharacterId)),
            TwoLines(TimeTrials.VehicleName(time.VehicleId), TimeTrials.DriftName(time.DriftType, time.DriftCategory)),
            Text(TimeTrials.ControllerName(time.ControllerType)),
            Text(FormatDate(time.DateSet), 12, "Neutral300"),
        ];

    private static string FormatDate(string? date) =>
        DateTime.TryParse(date, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed.ToString("d")
            : date ?? string.Empty;

    private Border Card(Control content) =>
        new()
        {
            Background = Brush("Neutral900", Brushes.Black),
            BorderBrush = Brush("Neutral800", Brushes.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = content,
        };

    #endregion

    #region Browse tracks

    private async Task LoadTracksAsync()
    {
        _body.Content = Message("Loading tracks…");
        var result = await _api.CallApiAsync(api => api.GetTimeTrialTracksAsync());
        if (result.IsFailure || result.Value is null)
        {
            _body.Content = Message("Could not reach rwfc.net. Try again in a moment.");
            return;
        }

        // Only the tracks this Retro Rewind has a ghost slot for, matched by name.
        _tracks.Clear();
        foreach (var kind in new[] { GhostKind.Retro, GhostKind.Custom })
        {
            foreach (var slot in _importer.LoadTracks(kind))
            {
                if (TimeTrials.BoardTrackFor(slot, result.Value) is { } board)
                    _tracks[board.Id] = (slot, board);
            }
        }

        if (
            _start is { } start
            && _tracks.Values.FirstOrDefault(track => track.Slot.Kind == start.Kind && track.Slot.Index == start.Index)
                is { Board: not null } opened
        )
        {
            // Back goes to that track's list, with the records loaded behind the board.
            _custom = start.Kind == GhostKind.Custom;
            ShowBoard(opened);
            await LoadRecordsAsync();
            return;
        }

        ShowBrowse();
        await LoadRecordsAsync();
    }

    private string Summary() =>
        string.Join(
            " • ",
            $"{_filter.Cc}cc",
            _noGlitch ? "No glitch" : "Unrestricted",
            _filter.Vehicles switch
            {
                TimeTrialVehicles.Karts => "Karts",
                TimeTrialVehicles.Bikes => "Bikes",
                _ => "All vehicles",
            },
            _filter.Shrooms switch
            {
                TimeTrialShrooms.ShroomlessOnly => "Shroomless only",
                TimeTrialShrooms.WithShrooms => "Shroomless excluded",
                _ => "All categories",
            }
        );

    private TextBlock? _summary;
    private TextBlock? _listTitle;
    private ContentControl? _recordsHost;

    private void ShowBrowse()
    {
        _track = null;
        _lastBrowse = 0;
        Say("Click a track to see its times. Ghosts go into your Retro Rewind ghost folder.");

        // "Browse tracks": the same filters as the website, in the same order.
        var filters = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
        };
        void Place(Control control, int column, int row)
        {
            Grid.SetColumn(control, column);
            Grid.SetRow(control, row);
            filters.Children.Add(control);
        }

        Place(
            Labelled(
                "Track category",
                Segmented(
                    ["Retro", "Custom"],
                    _custom ? 1 : 0,
                    i =>
                    {
                        _custom = i == 1;
                        ShowRecords();
                    }
                )
            ),
            0,
            0
        );
        Place(
            Labelled(
                "Engine class",
                Segmented(["150cc", "200cc"], _filter.Cc == 200 ? 1 : 0, i => Refilter(_filter with { Cc = i == 1 ? 200 : 150 }))
            ),
            1,
            0
        );
        Place(
            Labelled(
                "Category type",
                Segmented(
                    ["Unrestricted", "No glitch"],
                    _noGlitch ? 1 : 0,
                    i =>
                    {
                        _noGlitch = i == 1;
                        Refilter(_filter);
                    }
                )
            ),
            2,
            0
        );
        Place(
            Labelled(
                "Vehicle type",
                Segmented(["All", "Karts", "Bikes"], (int)_filter.Vehicles, i => Refilter(_filter with { Vehicles = (TimeTrialVehicles)i }))
            ),
            0,
            1
        );
        Place(
            Labelled(
                "Shroomless",
                Segmented(["All", "Only", "Exclude"], (int)_filter.Shrooms, i => Refilter(_filter with { Shrooms = (TimeTrialShrooms)i }))
            ),
            1,
            1
        );
        var search = new InputField
        {
            Text = _search,
            Placeholder = "Search by track name…",
            IconData = _owner.TryFindResource("Search", out var icon) ? icon as Geometry : null,
            Background = Brush("Neutral950", Brushes.Black),
        };
        search.TextChanged += (_, _) =>
        {
            _search = search.Text?.Trim() ?? string.Empty;
            ShowRecords();
        };
        // Most times on the board come without a ghost file, so this keeps only the ones that can be downloaded.
        Place(
            Labelled(
                "Ghosts",
                Segmented(
                    ["All", "Downloadable"],
                    _ghostsOnly ? 1 : 0,
                    i =>
                    {
                        _ghostsOnly = i == 1;
                        ShowRecords();
                    }
                )
            ),
            2,
            1
        );
        var searchBox = Labelled("Search tracks", search);
        Grid.SetColumnSpan(searchBox, 3);
        Place(searchBox, 0, 2);

        _summary = Text(Summary(), 12, "Neutral300");
        var browse = Card(
            new StackPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(18, 14, 18, 10),
                        Spacing = 2,
                        Children = { Text("Browse tracks", 18, "Neutral50", FontWeight.Bold), _summary },
                    },
                    new Border
                    {
                        Background = Brush("Neutral800", Brushes.Gray),
                        Padding = new Thickness(10, 8),
                        Child = filters,
                    },
                },
            }
        );

        // The tracks and their world records.
        _listTitle = Text(string.Empty, 18, "Neutral50", FontWeight.Bold);
        _recordsHost = new ContentControl();
        var header = HeaderRow(
            RecordColumns,
            "Track",
            "World record",
            "Record holder",
            "Character",
            "Vehicle",
            "Controller",
            "Date set",
            "Ghost"
        );
        header.Margin = new Thickness(16, 6, 16, 8);
        Grid.SetRow(header, 1);
        Grid.SetRow(_recordsHost, 2);
        var tableHead = new StackPanel
        {
            Margin = new Thickness(18, 14, 18, 4),
            Spacing = 2,
            Children = { _listTitle },
        };
        var table = Card(new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { tableHead, header, _recordsHost } });
        table.Margin = new Thickness(0, 14, 0, 0);

        Grid.SetRow(table, 1);
        _body.Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { browse, table } };
        ShowRecords();
    }

    private void Refilter(TimeTrialFilter filter)
    {
        _filter = filter;
        if (_summary is not null)
            _summary.Text = Summary();
        _ = LoadRecordsAsync();
    }

    private bool? GlitchQuery => _noGlitch ? false : null;

    private async Task LoadRecordsAsync()
    {
        var version = ++_recordsVersion;
        if (_recordsHost is not null)
            _recordsHost.Content = Message("Loading records…");

        var filter = _filter;
        var glitch = GlitchQuery;
        var result = await _api.CallApiAsync(api =>
            api.GetTimeTrialRecordsAsync(filter.Cc, glitch, filter.ShroomlessQuery, filter.VehicleQuery)
        );
        if (version != _recordsVersion)
            return;
        if (result.IsFailure || result.Value is null)
        {
            if (_recordsHost is not null)
                _recordsHost.Content = Message("Could not load the records. Try again in a moment.");
            return;
        }

        _records = result.Value;
        ShowRecords();
    }

    private void ShowRecords()
    {
        if (_recordsHost is null || _listTitle is null)
            return;

        var category = _custom ? "custom" : "retro";
        var rows = new List<(GhostTrack Slot, RwfcTimeTrialTrack Board, RwfcTimeTrialTime? Record)>();
        foreach (var entry in _records)
        {
            if (
                !_tracks.TryGetValue(entry.TrackId, out var track)
                || !track.Board.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
            )
                continue;
            if (_search.Length > 0 && !track.Slot.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (_ghostsOnly && entry.ActiveWorldRecord is not { HasGhostFile: true })
                continue;
            rows.Add((track.Slot, track.Board, entry.ActiveWorldRecord));
        }

        _listTitle.Text = $"{(_custom ? "Custom" : "Retro")} tracks · {rows.Count}";
        if (_records.Count == 0)
            return;
        if (rows.Count == 0)
        {
            _recordsHost.Content = Message("No track matches that.");
            return;
        }

        var list = new StackPanel();
        foreach (var (slot, board, record) in rows)
        {
            var trackCell = TwoLines(slot.Name, $"{board.Laps} laps", "Neutral50", FontWeight.SemiBold);
            var chosen = (slot, board);
            Control?[] cells = record is null
                ? [trackCell, Text("No record yet", 12, "Neutral500")]
                : [trackCell, Text(record.FinishTimeDisplay, 16, "Primary300", FontWeight.Bold), .. RunCells(record), GhostMark(record)];
            if (record is not null)
                ((TextBlock)cells[1]!).Foreground = Accent;
            list.Children.Add(TableRow(RecordColumns, cells, () => ShowBoard(chosen)));
        }

        _recordsHost.Content = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = list };
    }

    // ✓ when the record's ghost can be downloaded; RWFC only has the ghost file for some times.
    private Control GhostMark(RwfcTimeTrialTime record)
    {
        var mark = Text(record.HasGhostFile ? "✓" : "—", record.HasGhostFile ? 16 : 13, "Neutral500", FontWeight.Bold);
        if (record.HasGhostFile)
            mark.Foreground = Accent;
        ToolTip.SetTip(mark, record.HasGhostFile ? "This record's ghost can be downloaded" : "RWFC doesn't have this record's ghost");
        return mark;
    }

    #endregion

    #region Player rankings

    private ContentControl? _rankingsHost;
    private TextBlock? _rankingsCount;
    private TextBlock? _rankingsSummary;
    private readonly List<RwfcRankedPlayer> _ranked = [];
    private int _rankPage;
    private int _rankPages;
    private int _rankVersion;

    private string? RankCategoryQuery =>
        _rankCategory switch
        {
            1 => "retro",
            2 => "custom",
            _ => null,
        };

    /// <summary>Players ranked by the world records they hold, with the website's filters.</summary>
    private void ShowRankings()
    {
        _track = null;
        _lastBrowse = 1;
        _status.Foreground = Brush("Neutral300", Brushes.Gray);
        Say(string.Empty);

        var filters = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*") };
        void Place(Control control, int column)
        {
            Grid.SetColumn(control, column);
            filters.Children.Add(control);
        }

        void Changed(TimeTrialFilter filter)
        {
            _filter = filter;
            if (_rankingsSummary is not null)
                _rankingsSummary.Text = RankingSummary();
            _ = LoadRankingsAsync(reset: true);
        }

        Place(
            Labelled(
                "Engine class",
                Segmented(["150cc", "200cc"], _filter.Cc == 200 ? 1 : 0, i => Changed(_filter with { Cc = i == 1 ? 200 : 150 }))
            ),
            0
        );
        Place(
            Labelled(
                "Category type",
                Segmented(
                    ["Unrestricted", "No glitch"],
                    _noGlitch ? 1 : 0,
                    i =>
                    {
                        _noGlitch = i == 1;
                        Changed(_filter);
                    }
                )
            ),
            1
        );
        Place(
            Labelled(
                "Vehicle type",
                Segmented(["All", "Karts", "Bikes"], (int)_filter.Vehicles, i => Changed(_filter with { Vehicles = (TimeTrialVehicles)i }))
            ),
            2
        );
        Place(
            Labelled(
                "Shroomless",
                Segmented(["All", "Only", "Exclude"], (int)_filter.Shrooms, i => Changed(_filter with { Shrooms = (TimeTrialShrooms)i }))
            ),
            3
        );
        Place(
            Labelled(
                "Track category",
                Segmented(
                    ["All", "Retro", "Custom"],
                    _rankCategory,
                    i =>
                    {
                        _rankCategory = i;
                        Changed(_filter);
                    }
                )
            ),
            4
        );

        _rankingsSummary = Text(RankingSummary(), 12, "Neutral300");
        var filterCard = Card(
            new StackPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(18, 14, 18, 10),
                        Spacing = 2,
                        Children = { Text("Filters", 18, "Neutral50", FontWeight.Bold), _rankingsSummary },
                    },
                    new Border
                    {
                        Background = Brush("Neutral800", Brushes.Gray),
                        Padding = new Thickness(10, 8),
                        Child = filters,
                    },
                },
            }
        );

        _rankingsCount = Text(string.Empty, 12, "Neutral300");
        _rankingsHost = new ContentControl();
        var header = HeaderRow(RankingColumns, "Rank", "Player", "WRs");
        header.Margin = new Thickness(16, 6, 16, 8);
        var tableHead = new StackPanel
        {
            Margin = new Thickness(18, 14, 18, 4),
            Spacing = 2,
            Children = { Text("Player rankings", 18, "Neutral50", FontWeight.Bold), _rankingsCount },
        };
        Grid.SetRow(header, 1);
        Grid.SetRow(_rankingsHost, 2);
        var table = Card(new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { tableHead, header, _rankingsHost } });
        table.Margin = new Thickness(0, 14, 0, 0);
        Grid.SetRow(table, 1);
        _body.Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { filterCard, table } };
        _ = LoadRankingsAsync(reset: true);
    }

    private string RankingSummary() =>
        string.Join(
            " • ",
            new[] { $"{_filter.Cc}cc", _noGlitch ? "No glitch" : "Unrestricted" }.Concat(
                new[]
                {
                    _filter.Vehicles == TimeTrialVehicles.Karts ? "Karts"
                    : _filter.Vehicles == TimeTrialVehicles.Bikes ? "Bikes"
                    : null,
                    _filter.Shrooms == TimeTrialShrooms.ShroomlessOnly ? "Shroomless only"
                    : _filter.Shrooms == TimeTrialShrooms.WithShrooms ? "Shroomless excluded"
                    : null,
                    _rankCategory == 1 ? "Retro"
                    : _rankCategory == 2 ? "Custom"
                    : null,
                }.OfType<string>()
            )
        );

    private async Task LoadRankingsAsync(bool reset)
    {
        if (_rankingsHost is null)
            return;

        var version = ++_rankVersion;
        if (reset)
        {
            _ranked.Clear();
            _rankPage = 0;
            _rankPages = 0;
            _rankingsHost.Content = Message("Loading rankings…");
        }

        var filter = _filter;
        var glitch = GlitchQuery;
        var category = RankCategoryQuery;
        var page = _rankPage + 1;
        var result = await _api.CallApiAsync(api =>
            api.GetTimeTrialRankingsAsync(filter.Cc, glitch, filter.ShroomlessQuery, filter.VehicleQuery, category, page, 50)
        );
        if (version != _rankVersion)
            return;
        if (result.IsFailure || result.Value is null)
        {
            _rankingsHost.Content = Message("Could not load the rankings. Try again in a moment.");
            return;
        }

        _rankPage = page;
        _rankPages = result.Value.TotalPages;
        _ranked.AddRange(result.Value.Players);
        if (_rankingsCount is not null)
            _rankingsCount.Text = $"{result.Value.TotalPlayers} players with world records";
        ShowRanked();
    }

    private void ShowRanked()
    {
        if (_rankingsHost is null)
            return;
        if (_ranked.Count == 0)
        {
            _rankingsHost.Content = Message("Nobody holds a world record with these filters yet.");
            return;
        }

        var list = new StackPanel();
        foreach (var player in _ranked)
        {
            var count = Text(player.WorldRecordCount.ToString(), 16, "Neutral50", FontWeight.Bold);
            count.Foreground = Accent;
            count.HorizontalAlignment = HorizontalAlignment.Right;
            list.Children.Add(
                TableRow(
                    RankingColumns,
                    [
                        Text($"#{player.Rank}", 14, "Neutral50", FontWeight.Bold),
                        Holder(new RwfcTimeTrialTime { PlayerName = player.DisplayName, CountryAlpha2 = player.CountryAlpha2 }),
                        count,
                    ],
                    player.TtProfileId > 0 ? () => ShowProfile(player.TtProfileId) : null
                )
            );
        }

        if (_rankPage < _rankPages)
        {
            var more = new ActionButton
            {
                Text = "Load more",
                Tone = ActionButtonTone.Secondary,
                Size = ButtonSize.Compact,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10),
            };
            more.Click += async (_, _) =>
            {
                more.IsEnabled = false;
                await LoadRankingsAsync(reset: false);
            };
            list.Children.Add(more);
        }

        _rankingsHost.Content = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = list };
    }

    #endregion

    #region A player's profile

    private const string SubmissionColumns = "2*,0.8*,1.2*,0.9*,1.1*,1.4*,1.4*,0.9*,Auto";
    private const int ShownStep = 50;

    private int _profileId;
    private RwfcTimeTrialProfile? _profile;
    private readonly List<RwfcTimeTrialTime> _submissions = [];
    private int _profileVersion;
    private int _shown;
    private int _pCc;
    private int _pGlitch;
    private int _pVehicle;
    private int _pShrooms;
    private string _pSearch = string.Empty;
    private ContentControl? _submissionsHost;
    private TextBlock? _submissionsCount;
    private Grid? _profileStats;

    /// <summary>
    /// A player's time trial profile, like rwfc.net/timetrial/player: their records, every time they set with the site's
    /// filters, and a ghost button for each time that has one.
    /// </summary>
    private void ShowProfile(int id)
    {
        _track = null;
        _profileId = id;
        _profile = null;
        _status.Foreground = Brush("Neutral300", Brushes.Gray);
        Say(string.Empty);

        var back = new ActionButton
        {
            Text = "Back",
            Tone = ActionButtonTone.Secondary,
            Size = ButtonSize.Compact,
            IconData = _owner.TryFindResource("ArrowLeft", out var arrow) ? arrow as Geometry : null,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        back.Click += (_, _) =>
        {
            if (_lastBrowse == 1)
                ShowRankings();
            else
                ShowBrowse();
        };

        _profileStats = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var header = Card(Message("Loading the profile…"));
        header.Margin = new Thickness(0, 10, 0, 0);
        header.Name = "ProfileHeader";

        var filters = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        void Place(Control control, int column, int row = 0, int span = 1)
        {
            Grid.SetColumn(control, column);
            Grid.SetRow(control, row);
            Grid.SetColumnSpan(control, span);
            filters.Children.Add(control);
        }

        void Changed() => _ = LoadSubmissionsAsync();

        Place(
            Labelled(
                "Engine class",
                Segmented(
                    ["All", "150cc", "200cc"],
                    _pCc,
                    i =>
                    {
                        _pCc = i;
                        Changed();
                    }
                )
            ),
            0
        );
        Place(
            Labelled(
                "Glitch/Shortcut",
                Segmented(
                    ["All", "Non-glitch", "Glitch"],
                    _pGlitch,
                    i =>
                    {
                        _pGlitch = i;
                        Changed();
                    }
                )
            ),
            1
        );
        Place(
            Labelled(
                "Vehicle type",
                Segmented(
                    ["All", "Karts", "Bikes"],
                    _pVehicle,
                    i =>
                    {
                        _pVehicle = i;
                        Changed();
                    }
                )
            ),
            2
        );
        Place(
            Labelled(
                "Shroomless",
                Segmented(
                    ["All", "Only", "Exclude"],
                    _pShrooms,
                    i =>
                    {
                        _pShrooms = i;
                        Changed();
                    }
                )
            ),
            0,
            1
        );
        var search = new InputField
        {
            Text = _pSearch,
            Placeholder = "Search by track name…",
            IconData = _owner.TryFindResource("Search", out var icon) ? icon as Geometry : null,
            Background = Brush("Neutral950", Brushes.Black),
        };
        search.TextChanged += (_, _) =>
        {
            _pSearch = search.Text?.Trim() ?? string.Empty;
            _shown = ShownStep;
            ShowSubmissions();
        };
        Place(Labelled("Search tracks", search), 1, 1, 2);
        var filterCard = Card(
            new StackPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(18, 12, 18, 8),
                        Children = { Text("Filter submissions", 16, "Neutral50", FontWeight.Bold) },
                    },
                    new Border
                    {
                        Background = Brush("Neutral800", Brushes.Gray),
                        Padding = new Thickness(10, 8),
                        Child = filters,
                    },
                },
            }
        );
        filterCard.Margin = new Thickness(0, 12, 0, 0);

        _submissionsCount = Text(string.Empty, 12, "Neutral300");
        _submissionsHost = new ContentControl();
        var tableHeader = HeaderRow(
            SubmissionColumns,
            "Track",
            "Category",
            "Time",
            "Fastest lap",
            "Character",
            "Vehicle",
            "Controller",
            "Date",
            "Ghost"
        );
        tableHeader.Margin = new Thickness(16, 6, 16, 8);
        var tableHead = new StackPanel
        {
            Margin = new Thickness(18, 14, 18, 4),
            Spacing = 2,
            Children = { Text("All submissions", 18, "Neutral50", FontWeight.Bold), _submissionsCount },
        };
        Grid.SetRow(tableHeader, 1);
        Grid.SetRow(_submissionsHost, 2);
        var table = Card(
            new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { tableHead, tableHeader, _submissionsHost } }
        );
        table.Margin = new Thickness(0, 12, 0, 0);

        var top = new StackPanel { Children = { back, header, _profileStats, filterCard } };
        Grid.SetRow(table, 1);
        _body.Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { top, table } };
        _ = LoadProfileAsync(header);
    }

    private async Task LoadProfileAsync(Border header)
    {
        var id = _profileId;
        var result = await _api.CallApiAsync(api => api.GetTimeTrialProfileAsync(id));
        if (id != _profileId)
            return;
        if (result.IsFailure || result.Value is null)
        {
            header.Child = Message("Could not load this player. Try again in a moment.");
            return;
        }

        _profile = result.Value;
        var records = Text(result.Value.CurrentWorldRecords.ToString(), 30, "Neutral50", FontWeight.Bold);
        records.Foreground = Accent;
        records.HorizontalAlignment = HorizontalAlignment.Right;
        var right = new StackPanel { Children = { records, Text("WORLD RECORDS", 11, "Neutral300", FontWeight.Bold) } };
        ((TextBlock)right.Children[1]).HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(right, 1);
        var name = Holder(new RwfcTimeTrialTime { PlayerName = result.Value.DisplayName, CountryAlpha2 = result.Value.CountryAlpha2 });
        header.Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(18, 14),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { name, Text("Time trial profile", 12, "Neutral400") },
                },
                right,
            },
        };
        await LoadSubmissionsAsync();
    }

    // The two number cards under the profile: all times, and how many tracks they cover.
    private void ShowProfileStats()
    {
        if (_profileStats is null)
            return;
        _profileStats.Children.Clear();
        var cards = new[]
        {
            ("Total submissions", _submissions.Count),
            ("Tracks played", _submissions.Select(time => time.TrackId).Distinct().Count()),
        };
        for (var i = 0; i < cards.Length; i++)
        {
            var value = Text(cards[i].Item2.ToString(), 24, "Neutral50", FontWeight.Bold);
            value.Foreground = Accent;
            value.HorizontalAlignment = HorizontalAlignment.Center;
            var label = Text(cards[i].Item1.ToUpperInvariant(), 11, "Neutral300", FontWeight.Bold);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var card = Card(
                new StackPanel
                {
                    Margin = new Thickness(10, 12),
                    Spacing = 2,
                    Children = { value, label },
                }
            );
            Grid.SetColumn(card, i);
            _profileStats.Children.Add(card);
        }
    }

    /// <summary>
    /// Loads all the player's times for the chosen filters (a few pages at most), so the track search and the "tracks
    /// played" count cover every time, like on the website.
    /// </summary>
    private async Task LoadSubmissionsAsync()
    {
        if (_submissionsHost is null)
            return;
        var version = ++_profileVersion;
        var id = _profileId;
        int? cc = _pCc switch
        {
            1 => 150,
            2 => 200,
            _ => null,
        };
        bool? glitch = _pGlitch switch
        {
            1 => false,
            2 => true,
            _ => null,
        };
        var shrooms = new TimeTrialFilter(Shrooms: (TimeTrialShrooms)_pShrooms).ShroomlessQuery;
        var vehicle = new TimeTrialFilter(Vehicles: (TimeTrialVehicles)_pVehicle).VehicleQuery;
        _submissionsHost.Content = Message("Loading times…");

        var all = new List<RwfcTimeTrialTime>();
        for (var page = 1; page <= 20; page++)
        {
            var current = page;
            var result = await _api.CallApiAsync(api =>
                api.GetTimeTrialProfileSubmissionsAsync(id, cc, glitch, shrooms, vehicle, current, 100)
            );
            if (version != _profileVersion)
                return;
            if (result.IsFailure || result.Value is null)
            {
                _submissionsHost.Content = Message("Could not load the times. Try again in a moment.");
                return;
            }

            all.AddRange(result.Value.Submissions);
            if (current >= result.Value.TotalPages)
                break;
        }

        _submissions.Clear();
        _submissions.AddRange(all);
        _shown = ShownStep;
        ShowProfileStats();
        ShowSubmissions();
    }

    private void ShowSubmissions()
    {
        if (_submissionsHost is null)
            return;
        var matching = _submissions
            .Where(time => _pSearch.Length == 0 || time.TrackName.Contains(_pSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (_submissionsCount is not null)
            _submissionsCount.Text = $"{matching.Count} submissions";
        if (matching.Count == 0)
        {
            _submissionsHost.Content = Message("No times match these filters.");
            return;
        }

        var list = new StackPanel();
        foreach (var time in matching.Take(_shown))
        {
            var known = _tracks.TryGetValue(time.TrackId, out var track);
            var laps = known ? $"{track.Board.Laps} laps" : null;
            var timeText = Text(time.FinishTimeDisplay, 15, "Primary300", FontWeight.Bold);
            timeText.Foreground = Accent;
            Control timeCell = time.Shroomless
                ? new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { timeText, Badge("Shroomless", "#78350F", "#FBBF24") },
                }
                : timeText;
            var chosen = time;
            Control ghost = known ? GhostCell(track, chosen, ShowSubmissions, "Ghost") : Text("No slot", 12, "Neutral500");
            if (!known)
                ToolTip.SetTip(ghost, "Your Retro Rewind has no ghost slot for this track");

            // ▾ shows the lap splits under the row, like the site's expand button.
            var splits = Text(
                string.Join("    ", time.LapSplitsDisplay.Select((lap, index) => $"Lap {index + 1}  {lap}")),
                12,
                "Neutral300"
            );
            var details = new Border
            {
                IsVisible = false,
                Padding = new Thickness(0, 8, 0, 0),
                Child = splits,
            };
            var expand = new ActionButton
            {
                Text = "▾",
                Tone = ActionButtonTone.Secondary,
                Size = ButtonSize.Compact,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = time.LapSplitsDisplay.Count > 0,
            };
            ToolTip.SetTip(expand, "Lap splits");
            expand.Click += (_, e) =>
            {
                e.Handled = true;
                details.IsVisible = !details.IsVisible;
                expand.Text = details.IsVisible ? "▴" : "▾";
            };
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { expand, ghost },
            };
            Control?[] cells =
            [
                TwoLines(time.TrackName, laps, "Neutral50", FontWeight.SemiBold),
                time.Cc == 200 ? Badge("200cc", "#0C4A6E", "#38BDF8") : Badge("150cc", "#14532D", "#4ADE80"),
                timeCell,
                Text(time.FastestLapDisplay ?? string.Empty, 12, "Neutral200"),
                Text(TimeTrials.CharacterName(time.CharacterId)),
                TwoLines(TimeTrials.VehicleName(time.VehicleId), TimeTrials.DriftName(time.DriftType, time.DriftCategory)),
                Text(TimeTrials.ControllerName(time.ControllerType)),
                Text(FormatDate(time.DateSet), 12, "Neutral300"),
                actions,
            ];
            var row = TableRow(
                SubmissionColumns,
                cells,
                known && time.HasGhostFile ? () => _ = InstallAsync(track, chosen, null, ShowSubmissions) : null
            );
            // The row's own grid moves into a panel with the lap splits under it, so it is taken off the row first.
            var inner = row.Child!;
            row.Child = null;
            row.Child = new StackPanel { Children = { inner, details } };
            list.Children.Add(row);
        }

        if (matching.Count > _shown)
        {
            var more = new ActionButton
            {
                Text = "Load more",
                Tone = ActionButtonTone.Secondary,
                Size = ButtonSize.Compact,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10),
            };
            more.Click += (_, _) =>
            {
                _shown += ShownStep;
                ShowSubmissions();
            };
            list.Children.Add(more);
        }

        _submissionsHost.Content = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = list };
    }

    // A small rounded label in the site's colours: green 150cc, blue 200cc, orange Shroomless.
    private static Border Badge(string text, string background, string foreground) =>
        new()
        {
            Background = new SolidColorBrush(Color.Parse(background)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(9, 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse(foreground)),
            },
        };

    #endregion

    #region One track's board

    private ContentControl? _boardHost;
    private bool _fastestLap;

    private void ShowBoard((GhostTrack Slot, RwfcTimeTrialTrack Board) track)
    {
        _track = track;
        _fastestLap = false;

        var back = new ActionButton
        {
            Text = "All tracks",
            Tone = ActionButtonTone.Secondary,
            Size = ButtonSize.Compact,
            IconData = _owner.TryFindResource("ArrowLeft", out var arrow) ? arrow as Geometry : null,
            VerticalAlignment = VerticalAlignment.Center,
        };
        back.Click += (_, _) => ShowBrowse();
        var title = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Text(track.Slot.Name, 18, "Neutral50", FontWeight.Bold),
                Text($"{track.Board.Laps} laps • {Summary()}", 12, "Neutral300"),
            },
        };
        var ranking = Segmented(
            ["Fastest race", "Fastest lap"],
            0,
            i =>
            {
                _fastestLap = i == 1;
                _ = LoadBoardAsync(reset: true);
            }
        );
        ranking.Width = 260;
        ranking.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(title, 1);
        Grid.SetColumn(ranking, 2);
        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 14,
            Margin = new Thickness(18, 14),
            Children = { back, title, ranking },
        };

        var header = HeaderRow(BoardColumns, "Rank", "Time", "Player", "Character", "Vehicle", "Controller", "Date set", "Ghost");
        header.Margin = new Thickness(16, 0, 16, 8);
        _boardHost = new ContentControl();
        Grid.SetRow(header, 1);
        Grid.SetRow(_boardHost, 2);
        _body.Content = Card(new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { top, header, _boardHost } });
        _ = LoadBoardAsync(reset: true);
    }

    private async Task LoadBoardAsync(bool reset)
    {
        if (_track is not { } track || _boardHost is null)
            return;

        var version = ++_boardVersion;
        if (reset)
        {
            _times.Clear();
            _page = 0;
            _totalPages = 0;
            _boardHost.Content = Message("Loading times…");
        }

        var filter = _filter;
        var lap = _fastestLap;
        var page = _page + 1;
        var glitch = GlitchQuery;
        var result = await _api.CallApiAsync(api =>
            lap
                ? api.GetTimeTrialLapBoardAsync(
                    track.Board.Id,
                    filter.Cc,
                    glitch,
                    filter.ShroomlessQuery,
                    filter.VehicleQuery,
                    page,
                    TimeTrials.PageSize
                )
                : api.GetTimeTrialBoardAsync(
                    track.Board.Id,
                    filter.Cc,
                    glitch,
                    filter.ShroomlessQuery,
                    filter.VehicleQuery,
                    page,
                    TimeTrials.PageSize
                )
        );
        if (version != _boardVersion)
            return;
        if (result.IsFailure || result.Value is null)
        {
            _boardHost.Content = Message("Could not load the times. Try again in a moment.");
            return;
        }

        _page = page;
        _totalPages = result.Value.TotalPages;
        _times.AddRange(result.Value.Submissions);
        ShowTimes(track);
    }

    private void ShowTimes((GhostTrack Slot, RwfcTimeTrialTrack Board) track)
    {
        if (_boardHost is null)
            return;
        if (_times.Count == 0)
        {
            _boardHost.Content = Message("No times with these filters yet.");
            return;
        }

        var list = new StackPanel();
        foreach (var time in _times)
        {
            var shown =
                _fastestLap && !string.IsNullOrWhiteSpace(time.FastestLapDisplay) ? time.FastestLapDisplay! : time.FinishTimeDisplay;
            var timeText = Text(shown, 16, "Primary300", FontWeight.Bold);
            timeText.Foreground = Accent;
            var chosen = time;
            Control?[] cells =
            [
                Text(time.Rank is { } rank ? $"#{rank}" : "—", 13, "Neutral300", FontWeight.SemiBold),
                timeText,
                .. RunCells(time),
                GhostCell(track, chosen),
            ];
            list.Children.Add(TableRow(BoardColumns, cells, time.HasGhostFile ? () => _ = InstallAsync(track, chosen) : null));
        }

        if (_page < _totalPages)
        {
            var more = new ActionButton
            {
                Text = "Load more",
                Tone = ActionButtonTone.Secondary,
                Size = ButtonSize.Compact,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10),
            };
            more.Click += async (_, _) =>
            {
                more.IsEnabled = false;
                await LoadBoardAsync(reset: false);
            };
            list.Children.Add(more);
        }

        _boardHost.Content = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = list };
        _status.Foreground = Brush("Neutral300", Brushes.Gray);
        Say(
            _times.Any(time => !time.HasGhostFile)
                ? $"Press Use ghost to race that ghost on {track.Slot.Name} ({_filter.Cc}cc). Times marked \"No ghost\" have no ghost file on RWFC."
                : $"Press Use ghost to race that ghost on {track.Slot.Name} ({_filter.Cc}cc)."
        );
    }

    /// <summary>
    /// The last column: a "Use ghost" button (the same as clicking the row), "In use" once that ghost is on the track,
    /// or "No ghost" when the time came without one.
    /// </summary>
    private Control GhostCell(
        (GhostTrack Slot, RwfcTimeTrialTrack Board) track,
        RwfcTimeTrialTime time,
        Action? redraw = null,
        string label = "Use ghost"
    )
    {
        if (!time.HasGhostFile)
            return Text("No ghost", 12, "Neutral500");
        if (_inUse.TryGetValue((track.Board.Id, time.Cc), out var id) && id == time.Id)
        {
            var inUse = Text("✓ In use", 13, "Neutral50", FontWeight.SemiBold);
            inUse.Foreground = Accent;
            return inUse;
        }

        var use = new ActionButton
        {
            Text = label,
            Tag = label,
            Tone = label == "Ghost" ? ActionButtonTone.Primary : ActionButtonTone.Secondary,
            Size = ButtonSize.Compact,
            IconData = _owner.TryFindResource("Download", out var icon) ? icon as Geometry : null,
            VerticalAlignment = VerticalAlignment.Center,
        };
        use.Click += (_, e) =>
        {
            e.Handled = true;
            _ = InstallAsync(track, time, use, redraw);
        };
        _useButtons[time.Id] = use;
        return use;
    }

    private readonly Dictionary<int, ActionButton> _useButtons = [];

    private async Task InstallAsync(
        (GhostTrack Slot, RwfcTimeTrialTrack Board) track,
        RwfcTimeTrialTime time,
        ActionButton? button = null,
        Action? redraw = null
    )
    {
        if (_installing)
            return;
        _installing = true;
        button ??= _useButtons.GetValueOrDefault(time.Id);
        if (button is not null)
        {
            button.Text = "Downloading…";
            button.IsEnabled = false;
        }
        _status.Foreground = Brush("Neutral300", Brushes.Gray);
        Say($"Downloading {time.PlayerName}'s ghost…");
        try
        {
            var download = await _api.CallApiAsync(api => api.DownloadTimeTrialGhostAsync(time.Id));
            if (download.IsFailure || download.Value is null)
            {
                Say("Could not download that ghost. Try again in a moment.");
                ResetButton(button);
                return;
            }

            var data = await download.Value.ReadAsByteArrayAsync();
            var speed = time.Cc == 200 ? 200 : 150;
            var result = _importer.ImportData(data, track.Slot, speed);
            if (!result.Success)
            {
                Say(result.Message);
                ResetButton(button);
                return;
            }

            _inUse[(track.Board.Id, time.Cc)] = time.Id;
            if (redraw is not null)
                redraw();
            else
                ShowTimes(track);
            _status.Foreground = Accent;
            Say(
                $"✓ {time.PlayerName}'s {time.FinishTimeDisplay} is now your ghost on {track.Slot.Name} ({speed}cc). "
                    + "Race it in Time Trials. The old one is in the Replaced folder."
            );
            _installed();
        }
        finally
        {
            _installing = false;
        }
    }

    private static void ResetButton(ActionButton? button)
    {
        if (button is null)
            return;
        button.Text = button.Tag as string ?? "Use ghost";
        button.IsEnabled = true;
    }

    #endregion
}
