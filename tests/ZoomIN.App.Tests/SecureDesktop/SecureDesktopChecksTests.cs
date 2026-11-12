using Microsoft.Extensions.Logging;
using ZoomIN.App.SecureDesktop.Checks;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.SecureDesktop;

/// <summary>The check lines of the secure instance (M10, Q8), from faked Win32 answers.</summary>
public sealed class SecureDesktopChecksTests
{
    private static readonly PhysicalRect s_left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect s_right = new(1920, 0, 2560, 1440);
    private static readonly PhysicalPoint s_leftCentre = new(960, 540);
    private static readonly PhysicalPoint s_rightCentre = new(3200, 720);

    private readonly FakeProbe _probe = new();
    private readonly CapturingLog<SecureDesktopChecks> _log = new();
    private readonly SecureDesktopChecks _checks;

    public SecureDesktopChecksTests()
    {
        _checks = new SecureDesktopChecks(_probe, new FakeMonitors(("DISPLAY1", s_left), ("DISPLAY2", s_right)), _log);
    }

    [Fact]
    public void EverythingRight_IsLoggedAsInformation()
    {
        var overlay = new ProbedWindow(0x1234, "ZoomIN.Overlay", s_left);
        _probe.Overlays.Add(overlay);
        _probe.Top[s_leftCentre] = overlay;
        _probe.Affinities[0x1234] = SecureDesktopChecks.ExcludeFromCapture;
        _probe.Processes[42] = new ProbedProcess(true, true);

        RunBoth(new SecureCheckInputs(42, [], ExitShortcut: true, SnapshotShortcut: true));

        Assert.All(_log.Lines, line => Assert.Equal(LogLevel.Information, line.Level));
        Assert.Contains("desktop Winlogon", _log.Text(1151), StringComparison.Ordinal);
        Assert.Contains("DISPLAY1", _log.Text(1152), StringComparison.Ordinal);
        Assert.Contains("0x1234", _log.Text(1154), StringComparison.Ordinal);
        Assert.Contains("in a job: yes", _log.Text(1157), StringComparison.Ordinal);
        Assert.Contains("watchdog 42 running, in a job: yes", _log.Text(1158), StringComparison.Ordinal);
        Assert.Contains("0 shortcut(s) unavailable, exit shortcut registered: True, snapshot shortcut registered: True", _log.Text(1161), StringComparison.Ordinal);
    }

    [Fact]
    public void AWindowAboveTheOverlay_IsAWarning_WithItsClassOnly()
    {
        var overlay = new ProbedWindow(0x1234, "ZoomIN.Overlay", s_right);
        _probe.Overlays.Add(overlay);
        _probe.Top[s_rightCentre] = new ProbedWindow(0x99, "LogonUI Logon Window", s_right);
        _probe.Affinities[0x1234] = SecureDesktopChecks.ExcludeFromCapture;

        RunBoth(new SecureCheckInputs(42, [], true, true));

        var line = Assert.Single(_log.Lines, l => l.Id == 1153);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains("DISPLAY2", line.Text, StringComparison.Ordinal);
        Assert.Contains("LogonUI Logon Window", line.Text, StringComparison.Ordinal);

        // DISPLAY1 has no overlay of its own: nothing to check there.
        Assert.DoesNotContain(_log.Lines, l => l.Id is 1152 or 1153 && l.Text.Contains("DISPLAY1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0u, "0x0")]
    [InlineData(null, "unknown")]
    public void AnOverlayNotExcludedFromCapture_IsAWarning(uint? affinity, string shown)
    {
        _probe.Overlays.Add(new ProbedWindow(0x1234, "ZoomIN.Overlay", s_left));
        if (affinity is { } value)
        {
            _probe.Affinities[0x1234] = value;
        }

        RunBoth(new SecureCheckInputs(42, [], true, true));

        var line = Assert.Single(_log.Lines, l => l.Id == 1155);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains(shown, line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NoOverlay_NoWatchdog_AndRefusedShortcuts_AreReported()
    {
        _probe.InJob = null;

        RunBoth(new SecureCheckInputs(null, [HotkeyGesture.ToggleZoom, HotkeyGesture.ZoomIn], ExitShortcut: false, SnapshotShortcut: true));

        Assert.Contains(_log.Lines, l => l.Id == 1156);
        Assert.Contains("unknown", _log.Text(1157), StringComparison.Ordinal);
        Assert.Equal(LogLevel.Warning, Assert.Single(_log.Lines, l => l.Id == 1159).Level);
        Assert.Equal(2, _log.Lines.Count(l => l.Id == 1160 && l.Level == LogLevel.Warning));
        Assert.Contains("2 shortcut(s) unavailable, exit shortcut registered: False", _log.Text(1161), StringComparison.Ordinal);
    }

    [Fact]
    public void AFailingProbe_IsLogged_NotThrown()
    {
        _probe.Throw = true;

        RunBoth(new SecureCheckInputs(42, [], true, true));

        var error = Assert.Single(_log.Lines, line => line.Level == LogLevel.Error);
        Assert.Equal(1162, error.Id);
    }

    [Fact]
    public void AWatchdogThatIsGone_IsAWarning()
    {
        _probe.Processes[42] = new ProbedProcess(false, null);

        _checks.RunAtStart(new SecureCheckInputs(42, [], true, true));

        Assert.Equal(LogLevel.Warning, Assert.Single(_log.Lines, line => line.Id == 1169).Level);
        Assert.DoesNotContain(_log.Lines, line => line.Id == 1158);
    }

    // As the start watch calls them: the start part first, the overlays after the first image.
    private void RunBoth(SecureCheckInputs inputs)
    {
        _checks.RunAtStart(inputs);
        _checks.RunOverlays();
    }
}
