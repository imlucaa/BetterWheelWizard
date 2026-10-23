using System.Linq.Expressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NSubstitute;
using Testably.Abstractions.Testing;
using WheelWizard.CustomDistributions;
using WheelWizard.Ghosts;
using WheelWizard.RrRooms;
using WheelWizard.Settings.Views;
using WheelWizard.Shared;
using WheelWizard.Shared.Services;

namespace WheelWizard.UI.Test;

public class GhostDownloadWindowTests
{
    // Calls the fake API straight away, like the real caller does with the real one.
    private sealed class DirectCaller(IRwfcApi api) : IApiCaller<IRwfcApi>
    {
        public async Task<OperationResult<TResult>> CallApiAsync<TResult>(Expression<Func<IRwfcApi, Task<TResult>>> apiCall) =>
            await apiCall.Compile()(api);
    }

    private static byte[] Ghost(int minutes, int seconds, int milliseconds)
    {
        var data = new byte[0x100];
        "RKGD"u8.CopyTo(data);
        var packed = (minutes << 17) | (seconds << 10) | milliseconds;
        data[4] = (byte)(packed >> 16);
        data[5] = (byte)(packed >> 8);
        data[6] = (byte)packed;
        data[0x10] = 3;
        return data;
    }

    private static RwfcTimeTrialTime Record() =>
        new()
        {
            Id = 5117,
            PlayerName = "Ann",
            MiiName = "sraim",
            CountryAlpha2 = "JP",
            Cc = 150,
            FinishTimeDisplay = "1:23.860",
            VehicleId = 7,
            CharacterId = 17,
            ControllerType = 3,
            DriftType = 1,
            DriftCategory = 0,
            DateSet = "2026-09-18",
            Rank = 1,
            HasGhostFile = true,
        };

    private static void Settle()
    {
        for (var i = 0; i < 10; i++)
            Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<string> Texts(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty);

    private static void Click(Window window, string text)
    {
        var target = window.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);
        var point = target.TranslatePoint(new Point(4, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    [AvaloniaFact]
    public void Browse_ShowsTheRecordsLikeTheSite_AndClickingATimeInstallsItsGhost()
    {
        // A Retro Rewind install with one retro track.
        var files = new MockFileSystem();
        var root = files.Path.GetFullPath("/rr6");
        files.Directory.CreateDirectory(files.Path.Combine(root, "Ghosts", "ExpertsRT"));
        files.File.WriteAllText(files.Path.Combine(root, "Ghosts", GhostTrackTable.RetroFile), "SNES Mario Circuit 1 = 12345678\n");
        files.File.WriteAllText(files.Path.Combine(root, "Ghosts", GhostTrackTable.CustomFile), "");
        var paths = Substitute.For<ICustomDistributionPaths>();
        paths.RetroRewindFolderPath.Returns(root);
        var importer = new GhostImporter(paths, files);

        // RWFC with that track, its record, its board and the record's ghost.
        var api = Substitute.For<IRwfcApi>();
        var board = new RwfcTimeTrialTrack
        {
            Id = 1,
            Name = "SNES Mario Circuit 1",
            Category = "retro",
            Laps = 3,
        };
        api.GetTimeTrialTracksAsync().Returns([board]);
        api.GetTimeTrialRecordsAsync(150, null, null, null)
            .Returns(
                new List<RwfcTrackRecord>
                {
                    new()
                    {
                        TrackId = 1,
                        TrackName = board.Name,
                        ActiveWorldRecord = Record(),
                    },
                }
            );
        api.GetTimeTrialBoardAsync(1, 150, null, null, null, 1, TimeTrials.PageSize)
            .Returns(new RwfcTimeTrialBoard { Submissions = [Record()], TotalPages = 1 });
        api.DownloadTimeTrialGhostAsync(5117).Returns(_ => new ByteArrayContent(Ghost(1, 23, 860)));
        api.GetTimeTrialRankingsAsync(150, null, null, null, null, 1, 50)
            .Returns(
                new RwfcTimeTrialRankings
                {
                    Players =
                    [
                        new()
                        {
                            Rank = 1,
                            TtProfileId = 66,
                            DisplayName = "TheSuperArtistGamer",
                            CountryAlpha2 = "GB",
                            WorldRecordCount = 123,
                        },
                    ],
                    TotalPages = 1,
                    TotalPlayers = 150,
                }
            );

        var installed = 0;
        var window = new GhostDownloadWindow(importer, new DirectCaller(api), new Border(), () => installed++);
        window.Show();
        Settle();

        // The browse panel and the records table, with the same columns as rwfc.net/tt.
        var texts = Texts(window).ToList();
        Assert.Contains("Browse tracks", texts);
        Assert.Contains("150cc • Unrestricted • All vehicles • All categories", texts);
        Assert.Contains("WORLD RECORD", texts);
        Assert.Contains("SNES Mario Circuit 1", texts);
        Assert.Contains("1:23.860", texts);
        Assert.Contains("Birdo", texts);
        Assert.Contains("Wild Wing", texts);
        Assert.Contains("Hybrid Outside", texts);
        Assert.Contains("GameCube Controller", texts);
        // Whether the record's ghost can be downloaded.
        Assert.Contains("GHOST", texts);
        Assert.Contains("✓", texts);

        // A track opens its board; a time there installs its ghost into the track's slot.
        Click(window, "SNES Mario Circuit 1");
        Assert.Contains("#1", Texts(window));
        Click(window, "#1");

        var slot = importer.LoadTracks(GhostKind.Retro)[0];
        Assert.Equal(new TimeSpan(0, 0, 1, 23, 860), importer.CurrentTime(slot, 150));
        Assert.Equal(1, installed);
        // The row says so, instead of only the line at the bottom.
        Assert.Contains("✓ In use", Texts(window));

        // Player rankings, like the website's.
        Click(window, "Player rankings");
        var rankings = Texts(window).ToList();
        Assert.Contains("150 players with world records", rankings);
        Assert.Contains("TheSuperArtistGamer", rankings);
        Assert.Contains("123", rankings);
        Assert.Contains("WRS", rankings);

        // A player's profile: their records and every time they set, each with its ghost button.
        api.GetTimeTrialProfileAsync(66)
            .Returns(
                new RwfcTimeTrialProfile
                {
                    Id = 66,
                    DisplayName = "TheSuperArtistGamer",
                    CountryAlpha2 = "GB",
                    CurrentWorldRecords = 123,
                    TotalSubmissions = 1,
                }
            );
        var theirs = Record();
        theirs.TrackId = 1;
        theirs.TrackName = board.Name;
        api.GetTimeTrialProfileSubmissionsAsync(66, null, null, null, null, 1, 100)
            .Returns(
                new RwfcProfileSubmissions
                {
                    Submissions = [theirs],
                    TotalSubmissions = 1,
                    TotalPages = 1,
                }
            );
        Click(window, "#1");
        var profile = Texts(window).ToList();
        Assert.Contains("WORLD RECORDS", profile);
        Assert.Contains("TOTAL SUBMISSIONS", profile);
        Assert.Contains("TRACKS PLAYED", profile);
        Assert.Contains("1 submissions", profile);
        Assert.Contains("150cc", profile);
        Assert.Contains("SNES Mario Circuit 1", profile);
        window.Close();
    }

    [AvaloniaFact]
    public void OpenedFromATrack_ItShowsThatTracksBoard_AndAllTracksGoesToTheList()
    {
        var files = new MockFileSystem();
        var root = files.Path.GetFullPath("/rr6");
        files.Directory.CreateDirectory(files.Path.Combine(root, "Ghosts", "ExpertsRT"));
        files.File.WriteAllText(files.Path.Combine(root, "Ghosts", GhostTrackTable.RetroFile), "SNES Mario Circuit 1 = 12345678\n");
        files.File.WriteAllText(files.Path.Combine(root, "Ghosts", GhostTrackTable.CustomFile), "");
        var paths = Substitute.For<ICustomDistributionPaths>();
        paths.RetroRewindFolderPath.Returns(root);
        var importer = new GhostImporter(paths, files);

        var api = Substitute.For<IRwfcApi>();
        var board = new RwfcTimeTrialTrack
        {
            Id = 1,
            Name = "SNES Mario Circuit 1",
            Category = "retro",
            Laps = 3,
        };
        api.GetTimeTrialTracksAsync().Returns([board]);
        api.GetTimeTrialRecordsAsync(150, null, null, null)
            .Returns(
                new List<RwfcTrackRecord>
                {
                    new()
                    {
                        TrackId = 1,
                        TrackName = board.Name,
                        ActiveWorldRecord = Record(),
                    },
                }
            );
        api.GetTimeTrialBoardAsync(1, 150, null, null, null, 1, TimeTrials.PageSize)
            .Returns(new RwfcTimeTrialBoard { Submissions = [Record()], TotalPages = 1 });

        var window = new GhostDownloadWindow(
            importer,
            new DirectCaller(api),
            new Border(),
            () => { },
            importer.LoadTracks(GhostKind.Retro)[0]
        );
        window.Show();
        Settle();

        var texts = Texts(window).ToList();
        Assert.Contains("#1", texts);
        Assert.DoesNotContain("Browse tracks", texts);

        window
            .GetVisualDescendants()
            .OfType<WheelWizard.Views.Components.ActionButton>()
            .First(button => button.Text == "All tracks")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle();
        texts = Texts(window).ToList();
        Assert.Contains("Browse tracks", texts);
        Assert.Contains("SNES Mario Circuit 1", texts);
        window.Close();
    }
}
