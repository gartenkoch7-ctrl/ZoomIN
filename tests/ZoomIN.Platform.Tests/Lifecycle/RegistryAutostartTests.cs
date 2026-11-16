using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Uses a private test key, never the real Run key.</summary>
public sealed class RegistryAutostartTests : IDisposable
{
    // Own key per test: deleting a shared parent broke parallel test runs (other worktrees too).
    private readonly TestRegistry _registry = new("Run");

    private string Key => _registry.RootPath;

    [Fact]
    public void SetEnabled_WritesAndRemovesTheValue()
    {
        var autostart = new RegistryAutostart(Key, "\"C:\\ZoomIN\\ZoomIN.exe\"");

        Assert.False(autostart.IsEnabled);
        autostart.SetEnabled(true);
        Assert.True(autostart.IsEnabled);
        autostart.SetEnabled(false);
        Assert.False(autostart.IsEnabled);
    }

    [Fact]
    public void OtherPath_DoesNotCountAsEnabled()
    {
        new RegistryAutostart(Key, "\"C:\\Old\\ZoomIN.exe\"").SetEnabled(true);

        Assert.False(new RegistryAutostart(Key, "\"C:\\New\\ZoomIN.exe\"").IsEnabled);
    }

    [Fact]
    public void Exists_FindsAValueForAnyPath()
    {
        Assert.False(new RegistryAutostart(Key, "\"C:\\New\\ZoomIN.exe\"").Exists);
        new RegistryAutostart(Key, "\"C:\\Old\\ZoomIN.exe\"").SetEnabled(true);

        var current = new RegistryAutostart(Key, "\"C:\\New\\ZoomIN.exe\"");
        Assert.True(current.Exists);

        // Switching off removes it whatever it pointed at (migration and uninstall, ADR-0030).
        current.SetEnabled(false);
        Assert.False(current.Exists);
    }

    public void Dispose() => _registry.Dispose();
}
