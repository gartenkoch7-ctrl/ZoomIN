using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
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

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void ProfileFromBefore11_SwitchesToTheMargin_LikeTheMigratedSettings(string centerMouse)
    {
        // M17 integration: profiles are not migrated themselves. A schema-1 profile has no
        // MouseFollow, so it reads as the factory margin, matching the one-time switch of settings.json.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "Alt.json"), $$"""{ "SchemaVersion": 1, "CenterMouse": {{centerMouse}}, "Contrast": 1.5 }""");
        var store = new JsonProfileStore(NullLogger<JsonProfileStore>.Instance, _directory);
        var settings = SettingsMigration.Migrate(new AppSettings { SchemaVersion = 1, CenterMouse = true }).Settings;

        var applied = ProfileFields.ApplyDisplay(settings, store.Load("Alt")!);

        Assert.Equal(MouseFollowMode.Margin, applied.MouseFollow);
        Assert.Equal(1.5, applied.Contrast);
        Assert.Equal(AppSettings.CurrentSchemaVersion, applied.SchemaVersion);
        Assert.True(applied.MouseFollowHintPending); // the hint belongs to settings.json, not to the profile
        Assert.Null(applied.CenterMouse);

        store.Save("Alt", applied);
        Assert.DoesNotContain("CenterMouse", File.ReadAllText(Path.Combine(_directory, "Alt.json")), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
