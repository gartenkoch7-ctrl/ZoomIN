using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// The writing low-level mouse hook of the pointer fence (ADR-0028), separate from the read-only
/// activity hook. Per move it asks the fence; a refused move is swallowed (return 1) and the
/// pointer put on the allowed point, as ClipCursor would. That clamp is part of keeping the
/// pointer out, not a move on its own (docs/kb/eingabe-und-tracking.md, "Zeigerzaun").
/// The callback only computes: Windows removes hooks that answer slowly.
/// </summary>
internal sealed unsafe class Win32FenceHook : IDisposable
{
    // Each hook has its own callback delegate (kept in a field so the GC never collects it):
    // an old hook whose thread outlived its 2 s join must not answer for the new one.
    private readonly HOOKPROC _proc;
    private readonly IFenceHookTarget _target;
    private readonly LowLevelHookThread _thread;
    private volatile bool _disposed;

    private Win32FenceHook(IFenceHookTarget target)
    {
        _target = target;
        _proc = MouseProc;
        _thread = new LowLevelHookThread("ZoomIN Pointer Fence", (WINDOWS_HOOK_ID.WH_MOUSE_LL, _proc));
    }

    /// <summary>Null if Windows refused the hook.</summary>
    public static Win32FenceHook? TryInstall(IFenceHookTarget target)
    {
        var hook = new Win32FenceHook(target);
        if (hook._thread.AllInstalled)
        {
            return hook;
        }

        hook.Dispose();
        return null;
    }

    public void Dispose()
    {
        // Passes everything on from now on, even if the thread outlives its join.
        _disposed = true;
        _thread.Dispose();
    }

    private LRESULT MouseProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && !_disposed && (uint)wParam.Value == PInvoke.WM_MOUSEMOVE && Swallow((MSLLHOOKSTRUCT*)lParam.Value))
        {
            return new LRESULT(1);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    /// <returns>True if the move is swallowed; the pointer is then already on the allowed point.</returns>
    private bool Swallow(MSLLHOOKSTRUCT* info)
    {
        var requested = new PhysicalPoint(info->pt.X, info->pt.Y);

        // Inside the hook the pointer has not moved yet: this is where it comes from.
        var current = PInvoke.GetCursorPos(out var position) ? new PhysicalPoint(position.X, position.Y) : requested;
        if (!_target.OnMove(current, requested, out var allowed))
        {
            return false;
        }

        if (allowed != current)
        {
            // Floor, not a cast: truncation would move negative coordinates (monitors left of the
            // primary) one pixel towards 0, possibly onto a forbidden one.
            PInvoke.SetCursorPos((int)Math.Floor(allowed.X), (int)Math.Floor(allowed.Y));
        }

        return true;
    }
}
