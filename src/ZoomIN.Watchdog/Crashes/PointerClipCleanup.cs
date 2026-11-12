using Windows.Win32;

namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Frees the pointer after a crash while the pointer fence held ClipCursor (ADR-0028, M19).
/// Windows removes the fence's mouse hook with the process, but not the clip rectangle; FailFast
/// also skips ZoomIN's own exit handlers. The watchdog cannot tell whose clip it is, so it also
/// frees one another program (a game) set at that moment: a pointer trapped by a dead ZoomIN is
/// worse, and such programs set their clip again when activated. Harmless if nothing is confined.
/// </summary>
public sealed class PointerClipCleanup : ICleanupStep
{
    public string Name => "Release pointer clip";

    public unsafe void Run() => PInvoke.ClipCursor(null);
}
