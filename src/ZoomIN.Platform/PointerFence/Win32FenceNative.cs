using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>The real Windows calls behind <see cref="IFenceNative"/>.</summary>
internal sealed unsafe class Win32FenceNative : IFenceNative
{
    public bool ClipTo(PhysicalRect rect, out int error)
    {
        var bounds = new RECT { left = (int)rect.X, top = (int)rect.Y, right = (int)rect.Right, bottom = (int)rect.Bottom };
        if (PInvoke.ClipCursor(&bounds))
        {
            error = 0;
            return true;
        }

        error = Marshal.GetLastPInvokeError();
        return false;
    }

    public void Unclip() => PInvoke.ClipCursor((RECT*)null);

    public PhysicalRect? CurrentClip()
    {
        RECT bounds;
        return PInvoke.GetClipCursor(&bounds)
            ? new PhysicalRect(bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top)
            : null;
    }

    public PhysicalRect VirtualScreen() => new(
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));

    public IDisposable? InstallMoveHook(IFenceHookTarget target) => Win32FenceHook.TryInstall(target);

    public IDisposable StartWatcher(IFenceEvents events) => new Win32FenceWatcher(events);
}
