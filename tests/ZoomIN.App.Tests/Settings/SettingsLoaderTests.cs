using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Settings;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Platform.Settings;

namespace ZoomIN.App.Tests.Settings;

/// <summary>The start of an updated ZoomIN with a real settings.json of an older version (M17 A1).</summary>
public sealed class SettingsLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void OldFile_IsMigratedOnce_AndSavedWithoutTheOldField(string centerMouse)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, $$"""{ "SchemaVersion": 1, "LastZoomLevel": 3.0, "CenterMouse": {{centerMouse}} }""");

        var settings = Loader().Load();

        Assert.Equal(MouseFollowMode.Margin, settings.MouseFollow);
        Assert.True(settings.MouseFollowHintPending);
        Assert.Equal(3.0, settings.LastZoomLevel);
        var saved = File.ReadAllText(SettingsPath);
        Assert.DoesNotContain("CenterMouse", saved, StringComparison.Ordinal);
        Assert.Contains("\"SchemaVersion\": 2", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterTheMigration_TheUsersChoiceStays()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "SchemaVersion": 1, "CenterMouse": true }""");
        var migrated = Loader().Load();

        // The user saw the hint and chose the middle again.
        Store().Save(migrated with { MouseFollow = MouseFollowMode.Center, MouseFollowHintPending = false });
        var next = Loader().Load();

        Assert.Equal(MouseFollowMode.Center, next.MouseFollow);
        Assert.False(next.MouseFollowHintPending);
    }

    [Fact]
    public void FirstStart_WithoutFile_NeedsNoHint()
    {
        var settings = Loader().Load();

        Assert.Equal(MouseFollowMode.Margin, settings.MouseFollow);
        Assert.False(settings.MouseFollowHintPending);
        Assert.False(File.Exists(SettingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonSettingsStore Store() => new(NullLogger<JsonSettingsStore>.Instance, SettingsPath);

    private SettingsLoader Loader() => new(Store(), NullLogger<SettingsLoader>.Instance);
}
