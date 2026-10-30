using Microsoft.Win32;
using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Uses a private test key, never the real Run key.</summary>
public sealed class RegistryAutostartTests : IDisposable
{
    private readonly string _key = @"Software\ZoomIN.Tests\" + Guid.NewGuid().ToString("N");

    [Fact]
    public void SetEnabled_WritesAndRemovesTheValue()
    {
        var autostart = new RegistryAutostart(_key, "\"C:\\ZoomIN\\ZoomIN.exe\"");

        Assert.False(autostart.IsEnabled);
        autostart.SetEnabled(true);
        Assert.True(autostart.IsEnabled);
        autostart.SetEnabled(false);
        Assert.False(autostart.IsEnabled);
    }

    [Fact]
    public void OtherPath_DoesNotCountAsEnabled()
    {
        new RegistryAutostart(_key, "\"C:\\Old\\ZoomIN.exe\"").SetEnabled(true);

        Assert.False(new RegistryAutostart(_key, "\"C:\\New\\ZoomIN.exe\"").IsEnabled);
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\ZoomIN.Tests", throwOnMissingSubKey: false);
}
