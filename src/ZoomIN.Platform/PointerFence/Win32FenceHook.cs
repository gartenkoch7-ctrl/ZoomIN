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
    // One fence hook per process; the static callback needs a way back to it.
    private static readonly HOOKPROC s_proc = MouseProc;
    private static Win32FenceHook? s_active;

    private readonly IFenceHookTarget _target;
    private readonly LowLevelHookThread _thread;

    private Win32FenceHook(IFenceHookTarget target)
    {
        _target = target;
        Volatile.Write(ref s_active, this);
        _thread = new LowLevelHookThread("ZoomIN Pointer Fence", (WINDOWS_HOOK_ID.WH_MOUSE_LL, s_proc));
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
        _thread.Dispose();
        Interlocked.CompareExchange(ref s_active, null, this);
    }

    private static LRESULT MouseProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && (uint)wParam.Value == PInvoke.WM_MOUSEMOVE && Volatile.Read(ref s_active) is { } self)
        {
            var info = (MSLLHOOKSTRUCT*)lParam.Value;
            var requested = new PhysicalPoint(info->pt.X, info->pt.Y);

            // Inside the hook the pointer has not moved yet: this is where it comes from.
            var current = PInvoke.GetCursorPos(out var position) ? new PhysicalPoint(position.X, position.Y) : requested;
            if (self._target.OnMove(current, requested, out var allowed))
            {
                if (allowed != current)
                {
                    PInvoke.SetCursorPos((int)allowed.X, (int)allowed.Y);
                }

                return new LRESULT(1);
            }
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }
}
