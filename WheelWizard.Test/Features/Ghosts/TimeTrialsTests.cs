using System.Text.Json;
using WheelWizard.Ghosts;

namespace WheelWizard.Test.Features.Ghosts;

public class TimeTrialsTests
{
    private static RwfcTimeTrialTrack Board(int id, string name, bool hidden = false) =>
        new()
        {
            Id = id,
            Name = name,
            IsHidden = hidden,
        };

    [Fact]
    public void BoardTrack_IsFoundByName_IgnoringCaseSpacesAndSymbols()
    {
        var boards = new[] { Board(1, "SNES Mario Circuit 1"), Board(388, "SW2 Mario Circuit 1"), Board(50, "Poké Floats") };

        Assert.Equal(1, TimeTrials.BoardTrackFor(new GhostTrack(GhostKind.Retro, 0, "snes mario circuit 1", "AAAAAAAA"), boards)!.Id);
        Assert.Equal(50, TimeTrials.BoardTrackFor(new GhostTrack(GhostKind.Custom, 2, "Poké  Floats", "BBBBBBBB"), boards)!.Id);
        Assert.Null(TimeTrials.BoardTrackFor(new GhostTrack(GhostKind.Retro, 9, "GCN Baby Park", "CCCCCCCC"), boards));
    }

    [Fact]
    public void AHiddenBoardTrack_IsNotOffered()
    {
        var boards = new[] { Board(7, "Old Track", hidden: true) };

        Assert.Null(TimeTrials.BoardTrackFor(new GhostTrack(GhostKind.Custom, 0, "Old Track", "AAAAAAAA"), boards));
    }

    [Theory]
    [InlineData(TimeTrialVehicles.All, null)]
    [InlineData(TimeTrialVehicles.Karts, "karts")]
    [InlineData(TimeTrialVehicles.Bikes, "bikes")]
    public void VehicleFilter_UsesTheBoardsValues(TimeTrialVehicles vehicles, string? expected) =>
        Assert.Equal(expected, new TimeTrialFilter(Vehicles: vehicles).VehicleQuery);

    [Theory]
    [InlineData(TimeTrialShrooms.Any, null)]
    [InlineData(TimeTrialShrooms.ShroomlessOnly, "only")]
    [InlineData(TimeTrialShrooms.WithShrooms, "exclude")]
    public void ShroomFilter_UsesTheBoardsValues(TimeTrialShrooms shrooms, string? expected) =>
        Assert.Equal(expected, new TimeTrialFilter(Shrooms: shrooms).ShroomlessQuery);

    [Fact]
    public void Details_NameTheCharacterAndVehicle_AndTheKindOfRun()
    {
        var time = new RwfcTimeTrialTime
        {
            CharacterId = 22,
            VehicleId = 23,
            Shroomless = true,
            CountryAlpha2 = "JP",
        };

        Assert.Equal("Funky Kong · Flame Runner · Shroomless · JP", TimeTrials.Details(time));
        Assert.Equal("Mii", TimeTrials.CharacterName(30));
        Assert.Equal("?", TimeTrials.VehicleName(99));
    }

    [Fact]
    public void APlayersTimes_WithNoRank_AreRead()
    {
        // Taken from rwfc.net/api/timetrial/profile/2/submissions: a player's own times have "rank": null.
        const string json = """
            {"submissions":[{"id":5143,"trackId":70,"trackName":"GBA Bowser Castle 3","ttProfileId":2,"playerName":"Justin",
            "countryCode":388,"countryAlpha2":"JM","countryName":"Jamaica","cc":150,"finishTimeMs":141865,"finishTimeDisplay":"2:21.865",
            "vehicleId":23,"characterId":22,"controllerType":2,"driftType":0,"shroomless":true,"glitch":false,"isFlap":false,
            "driftCategory":1,"miiName":"faker jus","lapCount":3,"lapSplitsMs":[47613,47122,47130],
            "lapSplitsDisplay":["0:47.613","0:47.122","0:47.130"],"fastestLapMs":47122,"fastestLapDisplay":"0:47.122",
            "dateSet":"2026-09-15","submittedAt":"2026-10-09T01:18:00.075646Z","rank":null,"hasGhostFile":true}],
            "totalSubmissions":470,"currentPage":1,"pageSize":10,"totalPages":47}
            """;
        // The same settings the launcher's RWFC client uses.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var page = JsonSerializer.Deserialize<RwfcProfileSubmissions>(json, options)!;

        var time = Assert.Single(page.Submissions);
        Assert.Null(time.Rank);
        Assert.Equal(70, time.TrackId);
        Assert.Equal(["0:47.613", "0:47.122", "0:47.130"], time.LapSplitsDisplay);
        Assert.True(time.Shroomless);
        Assert.Equal(47, page.TotalPages);
    }
}
