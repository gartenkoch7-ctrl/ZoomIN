using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.SecureDesktop;

public sealed class SecureSettingsStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;

    private string User => Path.Combine(_root, "user");

    private string Machine => Path.Combine(_root, "machine");

    [Fact]
    public void UserSettings_AreUsed_AndKeptAsCopy()
    {
        WriteUser("""{"LastMode":"Lens","ZoomOn":true}""");

        var settings = Store(User).Load();

        Assert.Equal(ZoomMode.Lens, settings.LastMode);
        Assert.True(settings.ZoomOn);
        Assert.True(File.Exists(Path.Combine(Machine, "secure", "settings.json")));
    }

    [Fact]
    public void WithoutSignedInUser_TheCopyIsUsed()
    {
        WriteUser("""{"LastMode":"Docked"}""");
        _ = Store(User).Load();

        var settings = Store(userFolder: null).Load();

        Assert.Equal(ZoomMode.Docked, settings.LastMode);
    }

    [Fact]
    public void BrokenUserFile_FallsBackToTheCopy_AndKeepsIt()
    {
        WriteUser("""{"LastMode":"Lens"}""");
        _ = Store(User).Load();
        WriteUser("{ not json");

        var settings = Store(User).Load();

        Assert.Equal(ZoomMode.Lens, settings.LastMode);
        Assert.Equal(ZoomMode.Lens, Store(userFolder: null).Load().LastMode);
    }

    [Fact]
    public void NothingAvailable_GivesDefaults() =>
        Assert.Equal(ZoomMode.Fullscreen, Store(userFolder: null).Load().LastMode);

    [Fact]
    public void ValuesAreBounded()
    {
        WriteUser("""{"Contrast":99,"CursorColor":"red","MonitorZoomLevels":{"\\\\.\\DISPLAY1":1000}}""");

        var settings = Store(User).Load();

        Assert.Equal(3, settings.Contrast);
        Assert.Equal(new AppSettings().CursorColor, settings.CursorColor);
        Assert.Equal(16, settings.MonitorZoomLevels[@"\\.\DISPLAY1"]);
    }

    [Fact]
    public void Save_NeverWritesTheUserFile()
    {
        WriteUser("""{"LastMode":"Lens"}""");
        var store = Store(User);
        var settings = store.Load();

        store.Save(settings with { LastMode = ZoomMode.Docked });

        Assert.Equal("""{"LastMode":"Lens"}""", File.ReadAllText(Path.Combine(User, "settings.json")));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SecureSettingsStore Store(string? userFolder) => new(NullLog.Instance, SessionUser.ForFolder(userFolder), Machine);

    private void WriteUser(string json)
    {
        Directory.CreateDirectory(User);
        File.WriteAllText(Path.Combine(User, "settings.json"), json);
    }
}
