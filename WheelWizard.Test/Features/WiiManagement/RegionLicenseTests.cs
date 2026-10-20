using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;
using WheelWizard.CustomDistributions;
using WheelWizard.Dolphin.Paths;
using WheelWizard.Models.Enums;
using WheelWizard.RrRooms;
using WheelWizard.Settings;
using WheelWizard.Settings.Types;
using WheelWizard.Shared.Binary;
using WheelWizard.Shared.Polling;
using WheelWizard.WheelWizardData;
using WheelWizard.WiiManagement.GameLicense;
using WheelWizard.WiiManagement.MiiManagement;

namespace WheelWizard.Test.Features.WiiManagement;

public class RegionLicenseTests
{
    [Fact]
    public void RegionSummaries_KeepSparseSlotIndexes_AndDoNotChangeActiveData()
    {
        var (service, fs, settings) = Create();
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0, 3);
        AddSave(fs, MarioKartWiiEnums.Regions.Japan, 2);
        var users = service.LicenseCollection.Users;
        var profiles = service.GetRegionLicenses();
        Assert.Equal(3, profiles.Count);
        Assert.Equal(new[] { 0, 3 }, profiles.Where(p => p.Region == MarioKartWiiEnums.Regions.Europe).Select(p => p.Slot));
        Assert.Equal("Slot 3", profiles.Single(p => p.Region == MarioKartWiiEnums.Regions.Japan).Name);
        Assert.Same(users, service.LicenseCollection.Users);
        Assert.Empty(users);
        Assert.DoesNotContain(settings.ReceivedCalls(), call => call.GetMethodInfo().Name == "Set");
    }

    [Fact]
    public void RegionSummaries_IncludeAtMostFourProfilesPerRegion()
    {
        var (service, fs, _) = Create();
        foreach (var region in Enum.GetValues<MarioKartWiiEnums.Regions>().Where(r => r != MarioKartWiiEnums.Regions.None))
            AddSave(fs, region, 0, 1, 2, 3);
        Assert.Equal(16, service.GetRegionLicenses().Count);
    }

    [Fact]
    public void RegionSummaries_IgnoreTruncatedAndEmptySaves()
    {
        var (service, fs, _) = Create();
        AddSave(fs, MarioKartWiiEnums.Regions.Europe);
        fs.Directory.CreateDirectory("/saves/RMCJ");
        fs.File.WriteAllText("/saves/RMCJ/rksys.dat", "RKSD0006");
        Assert.Empty(service.GetRegionLicenses());
    }

    [Fact]
    public void ChangeProfileId_RenewsTheIds_MakesABackup_AndLeavesOtherLicensesAlone()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        settings.PathsSetupCorrectly().Returns(true);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0, 1);
        Assert.True(service.LoadLicense().IsSuccess);

        var result = service.ChangeProfileId(0, 200_000);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var ids = service.GetLicenseIdentities();
        var changed = ids.Single(i => i.Slot == 0);
        var other = ids.Single(i => i.Slot == 1);
        Assert.Equal(200_000u, changed.ProfileId);
        Assert.Equal(changed.PseudoId, changed.AuthenticId);
        Assert.NotEqual(other.PseudoId, changed.PseudoId);
        Assert.NotEqual(100u, changed.PseudoId);
        Assert.Equal(2u, other.ProfileId);
        Assert.Equal(101u, other.PseudoId);
        var backup = Assert.Single(fs.Directory.GetDirectories("/saves/RMCP/backup"));
        Assert.EndsWith("-change", backup);
        Assert.True(fs.File.Exists(fs.Path.Combine(backup, "rksys.dat")));
    }

    [Fact]
    public void ChangeProfileId_AlsoPutsTheSameCodeOnTheOtherRegionsSaves_AndUndoRevertsThem()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        settings.PathsSetupCorrectly().Returns(true);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0);
        AddSave(fs, MarioKartWiiEnums.Regions.America, 0);
        Assert.True(service.LoadLicense().IsSuccess);
        var americaBefore = fs.File.ReadAllBytes("/saves/RMCE/rksys.dat");

        Assert.True(service.ChangeProfileId(0, 200_000).IsSuccess);

        var america = fs.File.ReadAllBytes("/saves/RMCE/rksys.dat");
        Assert.Equal(200_000u, BigEndianBinary.BufferToUint32(america, 8 + 0x5C));
        Assert.Single(fs.Directory.GetDirectories("/saves/RMCE/backup", "*-change"));

        Assert.True(service.UndoLastProfileIdChange().IsSuccess);

        var americaAfter = fs.File.ReadAllBytes("/saves/RMCE/rksys.dat");
        Assert.Equal(BigEndianBinary.BufferToUint32(americaBefore, 8 + 0x5C), BigEndianBinary.BufferToUint32(americaAfter, 8 + 0x5C));
    }

    [Fact]
    public void UndoLastProfileIdChange_PutsTheIdsBack_AndKeepsOtherProgress()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        settings.PathsSetupCorrectly().Returns(true);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0, 1);
        Assert.True(service.LoadLicense().IsSuccess);
        var original = service.GetLicenseIdentities();
        Assert.True(service.ChangeProfileId(0, 200_000).IsSuccess);
        Assert.NotNull(service.GetLastBackupTime());

        var undone = service.UndoLastProfileIdChange();

        Assert.True(undone.IsSuccess, undone.IsFailure ? undone.Error.Message : null);
        Assert.Equal(original, service.GetLicenseIdentities());
        // The undo kept a copy of the changed state, and a second undo has nothing left to do.
        Assert.Single(fs.Directory.GetDirectories("/saves/RMCP/backup", "*-undo"));
        Assert.True(service.UndoLastProfileIdChange().IsFailure);
    }

    [Fact]
    public void GetSaveWriteTimeUtc_IsNullWithoutASave_AndTheFileTimeWithOne()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        Assert.Null(service.GetSaveWriteTimeUtc());

        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0);

        var written = service.GetSaveWriteTimeUtc();
        Assert.NotNull(written);
        Assert.Equal(fs.File.GetLastWriteTimeUtc("/saves/RMCP/rksys.dat"), written);
    }

    [Fact]
    public void BackupSaveNow_CopiesTheSave_WithoutBecomingTheUndoPoint()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0);
        service.LoadLicense();

        var result = service.BackupSaveNow();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Single(fs.Directory.GetDirectories("/saves/RMCP/backup", "*-manual"));
        Assert.Null(service.GetLastBackupTime());
    }

    [Fact]
    public void BackupSaveNow_TwiceInOneSecond_KeepsBothBackups()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0);
        service.LoadLicense();

        var first = service.BackupSaveNow();
        var second = service.BackupSaveNow();

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(2, fs.Directory.GetDirectories("/saves/RMCP/backup", "*-manual").Length);
    }

    [Fact]
    public void RestoreExportAndImport_RoundTripTheWholeSave()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        settings.PathsSetupCorrectly().Returns(true);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0, 1);
        service.LoadLicense();
        var original = fs.File.ReadAllBytes("/saves/RMCP/rksys.dat");

        fs.Directory.CreateDirectory("/export");
        // Export writes a plain copy, import brings it back and backs up what was there.
        Assert.True(service.ExportSave("/export/rksys.dat").IsSuccess);
        Assert.Equal(original, fs.File.ReadAllBytes("/export/rksys.dat"));
        Assert.True(service.ChangeProfileId(0, 200_000).IsSuccess);
        Assert.NotEqual(original, fs.File.ReadAllBytes("/saves/RMCP/rksys.dat"));
        Assert.True(service.ImportSave("/export/rksys.dat").IsSuccess);
        Assert.Equal(1u, service.GetLicenseIdentities().Single(i => i.Slot == 0).ProfileId);
        Assert.Single(fs.Directory.GetDirectories("/saves/RMCP/backup", "*-before-import"));

        // Restore puts a chosen backup back, whole.
        var change = service.ListBackups().Single(b => b.Kind == "change");
        Assert.True(service.RestoreBackup(change.Name).IsSuccess);
        Assert.Equal(1u, service.GetLicenseIdentities().Single(i => i.Slot == 0).ProfileId);
        Assert.Single(fs.Directory.GetDirectories("/saves/RMCP/backup", "*-before-restore"));
    }

    [Fact]
    public void GetBackupLicenses_ShowsTheFriendCodesInsideABackup()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        settings.PathsSetupCorrectly().Returns(true);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0, 1);
        service.LoadLicense();
        var before = service.GetLicenseIdentities();
        Assert.True(service.ChangeProfileId(0, 200_000).IsSuccess);

        var backup = service.ListBackups().Single(b => b.Kind == "change");
        var licenses = service.GetBackupLicenses(backup.Name);

        Assert.Equal(2, licenses.Count);
        Assert.Equal(before.Select(i => i.FriendCode), licenses.Select(l => l.FriendCode));
        Assert.Equal("Slot 1", licenses[0].Name);
    }

    [Fact]
    public void ImportSave_RefusesAFileThatIsNotASave()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0);
        service.LoadLicense();
        fs.Directory.CreateDirectory("/import");
        fs.File.WriteAllText("/import/rksys.dat", "not a save");

        Assert.True(service.ImportSave("/import/rksys.dat").IsFailure);
        Assert.False(fs.Directory.Exists("/saves/RMCP/backup"));
    }

    [Fact]
    public void ChangeProfileId_RefusesAProfileIdAnotherLicenseUses()
    {
        var (service, fs, settings) = Create();
        settings.Get<MarioKartWiiEnums.Regions>(Arg.Any<Setting<MarioKartWiiEnums.Regions>>()).Returns(MarioKartWiiEnums.Regions.Europe);
        settings.PathsSetupCorrectly().Returns(true);
        AddSave(fs, MarioKartWiiEnums.Regions.Europe, 0, 1);
        service.LoadLicense();

        var result = service.ChangeProfileId(0, 2);

        Assert.True(result.IsFailure);
        Assert.False(fs.Directory.Exists("/saves/RMCP/backup"));
    }

    private static void AddSave(MockFileSystem fs, MarioKartWiiEnums.Regions region, params int[] slots)
    {
        var data = new byte[0x2BC000];
        Encoding.ASCII.GetBytes("RKSD0006").CopyTo(data, 0);
        foreach (var slot in slots)
        {
            var offset = 8 + slot * 0x8CC0;
            Encoding.ASCII.GetBytes("RKPD").CopyTo(data, offset);
            Encoding.BigEndianUnicode.GetBytes($"Slot {slot + 1}").CopyTo(data, offset + 0x14);
            BigEndianBinary.WriteUInt32BigEndian(data, offset + 0x5C, (uint)(slot + 1));
            BigEndianBinary.WriteUInt32BigEndian(data, offset + 0x4C, (uint)(100 + slot));
            BigEndianBinary.WriteUInt32BigEndian(data, offset + 0x58, (uint)(100 + slot));
        }
        var path = $"/saves/{GameRegion.GetGameId(region)}/rksys.dat";
        fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(path)!);
        fs.File.WriteAllBytes(path, data);
    }

    private static (GameLicenseSingletonService, MockFileSystem, ISettingsManager) Create()
    {
        var fs = new MockFileSystem();
        var paths = Substitute.For<ICustomDistributionPaths>();
        paths.SaveFolderPath.Returns("/saves");
        var settings = Substitute.For<ISettingsManager>();
        var miis = Substitute.For<IMiiDbService>();
        miis.GetByAvatarId(Arg.Any<uint>()).Returns(Fail("Missing Mii"));
        var service = new GameLicenseSingletonService(
            miis,
            fs,
            Substitute.For<IWhWzDataSingletonService>(),
            Substitute.For<IRrRatingReader>(),
            settings,
            Substitute.For<ISaveRegionService>(),
            Substitute.For<IDolphinPaths>(),
            paths,
            Substitute.For<IRoomPresence>(),
            Substitute.For<IPollingScheduler>(),
            System.TimeProvider.System,
            NullLogger<GameLicenseSingletonService>.Instance
        );
        return (service, fs, settings);
    }
}
