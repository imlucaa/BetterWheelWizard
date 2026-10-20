using NSubstitute;
using Testably.Abstractions.Testing;
using WheelWizard.ApplicationData;
using WheelWizard.Mods;

namespace WheelWizard.Test.Features.Mods;

public class ModPresetStoreTests
{
    private static (ModPresetStore Store, MockFileSystem Fs) Create()
    {
        var fs = new MockFileSystem();
        var location = Substitute.For<IApplicationDataLocation>();
        location.DirectoryPath.Returns("/app");
        return (new ModPresetStore(location, fs), fs);
    }

    [Fact]
    public void Load_WithoutAFile_IsEmpty()
    {
        var (store, _) = Create();

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_ReturnsThePatchTitlesInOrder()
    {
        var (store, _) = Create();

        Assert.True(store.Save("Racing", ["Mod B", "Mod A"]).IsSuccess);

        var loaded = store.Load();
        Assert.Equal(["Mod B", "Mod A"], loaded["Racing"]);
    }

    [Fact]
    public void Save_WithTheSameNameInAnotherCase_ReplacesThePreset()
    {
        var (store, _) = Create();
        store.Save("Racing", ["Old"]);

        store.Save("racing", ["New"]);

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal(["New"], loaded["Racing"]);
    }

    [Fact]
    public void Save_WithABlankName_Fails()
    {
        var (store, _) = Create();

        Assert.True(store.Save("   ", ["A"]).IsFailure);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Delete_RemovesOnlyThatPreset_AndUnknownNamesAreFine()
    {
        var (store, _) = Create();
        store.Save("One", ["A"]);
        store.Save("Two", ["B"]);

        Assert.True(store.Delete("one").IsSuccess);
        Assert.True(store.Delete("never saved").IsSuccess);

        Assert.Equal(["Two"], store.Load().Keys);
    }

    [Fact]
    public void Load_WithACorruptFile_IsEmptyInsteadOfThrowing()
    {
        var (store, fs) = Create();
        fs.Directory.CreateDirectory("/app");
        fs.File.WriteAllText("/app/ModPresets.json", "{ not json");

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_AfterACorruptFile_StartsFresh()
    {
        var (store, fs) = Create();
        fs.Directory.CreateDirectory("/app");
        fs.File.WriteAllText("/app/ModPresets.json", "{ not json");

        Assert.True(store.Save("Fresh", ["A"]).IsSuccess);

        Assert.Equal(["A"], store.Load()["Fresh"]);
    }
}
