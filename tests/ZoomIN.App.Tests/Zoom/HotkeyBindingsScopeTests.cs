using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Tests.Zoom;

/// <summary>The secure desktop registers only the core shortcuts (M10 Abschluss).</summary>
public sealed class HotkeyBindingsScopeTests
{
    [Fact]
    public void SecureDesktop_SkipsIslandFreezeAndMonitorShortcuts()
    {
        var (bindings, hotkeys, _) = HotkeyBindingsTests.Create(new AppSettings(), scope: EngineScope.SecureDesktop);

        bindings.Register();

        Assert.DoesNotContain(HotkeyGesture.ToggleIslands, hotkeys.Registered.Keys);
        Assert.DoesNotContain(HotkeyGesture.EditIslands, hotkeys.Registered.Keys);
        Assert.DoesNotContain(HotkeyGesture.FreezeMonitor, hotkeys.Registered.Keys);
        Assert.DoesNotContain(HotkeyGesture.NextMonitor, hotkeys.Registered.Keys);
        Assert.Contains(HotkeyGesture.ToggleZoom, hotkeys.Registered.Keys);
        Assert.Contains(HotkeyGesture.ZoomIn, hotkeys.Registered.Keys);
    }

    [Fact]
    public void SecureDesktop_DoesNotReportTheSkippedOnesAsUnavailable()
    {
        // Another application owning Ctrl+Alt+H must not show up: the shortcut is not asked for at all.
        var (bindings, _, _) = HotkeyBindingsTests.Create(
            new AppSettings(), taken: HotkeyGesture.FreezeMonitor, scope: EngineScope.SecureDesktop);

        bindings.Register();

        Assert.Empty(bindings.Unavailable);
    }

    [Fact]
    public void Full_RegistersThem_AndReportsATakenOne()
    {
        var (bindings, hotkeys, _) = HotkeyBindingsTests.Create(
            new AppSettings(), taken: HotkeyGesture.NextMonitor, scope: EngineScope.Full);

        bindings.Register();

        Assert.Contains(HotkeyGesture.ToggleIslands, hotkeys.Registered.Keys);
        Assert.Contains(HotkeyGesture.FreezeMonitor, hotkeys.Registered.Keys);
        Assert.Equal([HotkeyGesture.NextMonitor], bindings.Unavailable);
    }
}
