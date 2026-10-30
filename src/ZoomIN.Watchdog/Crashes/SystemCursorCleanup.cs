using Windows.Win32;

namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Makes sure the system pointer is visible after a crash while ZoomIN had hidden it (ADR-0011).
/// Harmless if Windows already restored it when the process ended.
/// </summary>
public sealed class SystemCursorCleanup : ICleanupStep
{
    public string Name => "Show system pointer";

    public void Run()
    {
        if (PInvoke.MagInitialize())
        {
            PInvoke.MagShowSystemCursor(true);
            PInvoke.MagUninitialize();
        }
    }
}
