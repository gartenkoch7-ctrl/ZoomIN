using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// Finds a visible Windows notification (toast). The shell draws toasts – including those of
/// Teams set to "Windows notifications" – in a CoreWindow of ShellExperienceHost. The same
/// process keeps further CoreWindows (notification centre, calendar), mostly cloaked; the size
/// rule in <see cref="NotificationRules"/> tells them apart. Runs on the tracking thread.
/// </summary>
internal sealed unsafe class ToastWindowFinder
{
    private const string WindowClass = "Windows.UI.Core.CoreWindow";
    private const string ShellProcess = "ShellExperienceHost";

    private readonly ILog _log;
    private readonly Dictionary<uint, bool> _shellProcesses = [];
    private HWND _logged;

    public ToastWindowFinder(ILog log)
    {
        _log = log;
    }

    /// <summary>The topmost toast on screen, or null.</summary>
    public PhysicalRect? Find()
    {
        // FindWindowEx walks the top-level windows in z-order, so the first match is the topmost.
        var window = HWND.Null;
        while (!(window = PInvoke.FindWindowEx(HWND.Null, window, WindowClass, null)).IsNull)
        {
            if (Visible(window) && IsShell(window) && Bounds(window) is { } rect)
            {
                LogFirst(window, rect);
                return rect;
            }
        }

        return null;
    }

    private static bool Visible(HWND window)
    {
        if (!PInvoke.IsWindowVisible(window))
        {
            return false;
        }

        // Hidden shell surfaces stay "visible" but cloaked by DWM.
        uint cloaked = 0;
        return PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Failed
            || cloaked == 0;
    }

    private bool IsShell(HWND window)
    {
        uint processId = 0;
        if (PInvoke.GetWindowThreadProcessId(window, &processId) == 0)
        {
            // The window has just been destroyed.
            return false;
        }

        if (!_shellProcesses.TryGetValue(processId, out var shell))
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                shell = string.Equals(process.ProcessName, ShellProcess, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                // The process has just exited.
                return false;
            }

            _shellProcesses[processId] = shell;
        }

        return shell;
    }

    private static PhysicalRect? Bounds(HWND window)
    {
        if (!PInvoke.GetWindowRect(window, out var r))
        {
            return null;
        }

        var rect = new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top);
        var monitor = PInvoke.MonitorFromWindow(window, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!PInvoke.GetMonitorInfo(monitor, &info))
        {
            return null;
        }

        var m = info.rcMonitor;
        return NotificationRules.LooksLikeToast(rect, new PhysicalRect(m.left, m.top, m.right - m.left, m.bottom - m.top))
            ? rect
            : null;
    }

    // Once per window: lets a log from another PC confirm (or refute) what the detection keys on.
    private void LogFirst(HWND window, PhysicalRect rect)
    {
        if (window != _logged)
        {
            _logged = window;
            _log.Info($"Notification window found: {WindowClass} of {ShellProcess}, {rect.Width}x{rect.Height} at {rect.X},{rect.Y}.");
        }
    }
}
