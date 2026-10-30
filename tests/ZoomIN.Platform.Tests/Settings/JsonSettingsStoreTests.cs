using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.Settings;

namespace ZoomIN.Platform.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Load_WithoutFile_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        Assert.Equal(new AppSettings(), store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);
        var settings = new AppSettings
        {
            LastZoomLevel = 3.5,
            AnimationMilliseconds = 0,
            ToggleHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x51),
            WheelModifiers = HotkeyModifiers.Alt,
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Save_WritesReadableModifierNames()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        store.Save(new AppSettings());

        // Names instead of numbers keep the file editable by hand; flag order is up to the serializer.
        Assert.Matches("\"Modifiers\": \"(Alt, Control|Control, Alt)\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Load_BrokenFile_ReturnsDefaults_AndKeepsBackup()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ this is not json");
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        var settings = store.Load();

        Assert.Equal(new AppSettings(), settings);
        Assert.True(File.Exists(SettingsPath + ".broken"));
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        var store = new JsonSettingsStore(NullLog.Instance, SettingsPath);

        store.Save(new AppSettings());

        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
