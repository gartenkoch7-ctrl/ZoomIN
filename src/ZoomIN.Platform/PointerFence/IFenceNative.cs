using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// What <see cref="Win32PointerFence"/> needs from Windows. Behind an interface so the release
/// in every failure path can be tested without confining the real pointer.
/// </summary>
internal interface IFenceNative
{
    /// <summary>ClipCursor(rect).</summary>
    /// <param name="error">The Win32 error if it failed (e.g. access denied while another desktop is active).</param>
    bool ClipTo(PhysicalRect rect, out int error);

    /// <summary>ClipCursor(NULL): the pointer may go anywhere again.</summary>
    void Unclip();

    /// <summary>GetClipCursor; null if it cannot be read.</summary>
    PhysicalRect? CurrentClip();

    /// <summary>GetCursorPos; null if it cannot be read (another desktop).</summary>
    PhysicalPoint? CursorPosition();

    /// <summary>The whole virtual screen: what GetClipCursor reports while nothing is confined.</summary>
    PhysicalRect VirtualScreen();

    /// <summary>Installs the low-level mouse hook that asks <paramref name="target"/> per move; null if Windows refused.</summary>
    IDisposable? InstallMoveHook(IFenceHookTarget target);

    /// <summary>Starts watching desktop switches, session unlock, display changes and the clip check.</summary>
    IDisposable StartWatcher(IFenceEvents events);
}
