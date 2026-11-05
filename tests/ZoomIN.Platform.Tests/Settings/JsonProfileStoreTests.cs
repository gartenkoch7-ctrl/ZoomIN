using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.Tests.Settings;

public sealed class JsonProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveLoad_RoundTrips_AndListsNames()
    {
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, _directory);

        store.Save("Büro", new AppSettings { ColorFilter = ColorFilter.Grayscale });
        store.Save("Abends", new AppSettings());

        Assert.Equal(["Abends", "Büro"], store.Names());
        Assert.Equal(ColorFilter.Grayscale, store.Load("Büro")!.ColorFilter);
    }

    [Fact]
    public void InvalidNames_AreRejected()
    {
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, _directory);

        store.Save("../evil", new AppSettings());

        Assert.Empty(store.Names());
        Assert.Null(store.Load("../evil"));
    }

    [Fact]
    public void Rename_FailsWhenTargetExists()
    {
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, _directory);
        store.Save("A", new AppSettings());
        store.Save("B", new AppSettings());

        Assert.False(store.Rename("A", "B"));
        Assert.True(store.Rename("A", "C"));
        Assert.Equal(["B", "C"], store.Names());
    }

    [Fact]
    public void ExportImport_CreatesUniqueName()
    {
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, _directory);
        store.Save("Büro", new AppSettings { Contrast = 1.5 });
        var file = Path.Combine(_directory, "..", Guid.NewGuid().ToString("N") + "-Büro.json");
        var exportFile = Path.Combine(Path.GetDirectoryName(file)!, "Büro.json");

        store.Export("Büro", exportFile);
        var imported = store.Import(exportFile);

        Assert.Equal("Büro 2", imported);
        Assert.Equal(1.5, store.Load("Büro 2")!.Contrast);
        File.Delete(exportFile);
    }

    [Fact]
    public void Import_InvalidFile_ReturnsNull()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "broken.txt");
        File.WriteAllText(file, "no json");
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, Path.Combine(_directory, "profiles"));

        Assert.Null(store.Import(file));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
