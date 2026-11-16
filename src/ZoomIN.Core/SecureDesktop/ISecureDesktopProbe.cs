using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// The Win32 questions behind the checks of the secure instance (M10, Q8): which desktop it runs
/// on, which window is on top, whether the overlays are excluded from screen capture and which
/// processes run in a job. Read-only; implemented in Platform, faked in tests.
/// </summary>
public interface ISecureDesktopProbe
{
    /// <summary>Name of the desktop this thread runs on ("Winlogon", "Default"), or null.</summary>
    string? DesktopName();

    /// <summary>This process's visible zoom overlays (window class of the overlay).</summary>
    IReadOnlyList<ProbedWindow> OwnOverlays();

    /// <summary>
    /// The visible window highest in the z-order that covers <paramref name="point"/>, including
    /// click-through ones (WindowFromPoint would skip the overlay itself); null if none.
    /// </summary>
    ProbedWindow? TopWindowAt(PhysicalPoint point);

    /// <summary>GetWindowDisplayAffinity of the window, or null if that failed.</summary>
    uint? DisplayAffinity(nint window);

    /// <summary>This process runs in a job object; null if that could not be asked.</summary>
    bool? IsInJob();

    /// <summary>Whether the process is still running and in a job.</summary>
    ProbedProcess Process(int processId);
}
