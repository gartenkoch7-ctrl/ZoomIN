using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// The caret as the classic accessibility interface (MSAA, OBJID_CARET) reports it. Besides the
/// system caret this covers applications that publish a caret object for magnifiers without a
/// real Win32 caret – notably Chromium once its accessibility is on (see ChromiumAccessibility).
/// </summary>
internal static class MsaaCaret
{
    private const int StateInvisible = 0x8000;

    public static PhysicalRect? Read(HWND window)
    {
        if (window.IsNull)
        {
            return null;
        }

        IAccessible? caret = null;
        try
        {
            if (PInvoke.AccessibleObjectFromWindow(window, unchecked((uint)OBJECT_IDENTIFIER.OBJID_CARET), out caret).Failed || caret is null)
            {
                return null;
            }

            object self = (int)PInvoke.CHILDID_SELF;
            if (caret.get_accState(self) is int state && (state & StateInvisible) != 0)
            {
                return null;
            }

            caret.accLocation(out var left, out var top, out var width, out var height, self);
            return width <= 0 && height <= 0
                ? null
                : new PhysicalRect(left, top, Math.Max(width, 1), Math.Max(height, 1));
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            // No caret object, or the window just closed.
            return null;
        }
        finally
        {
            if (caret is not null)
            {
                Marshal.ReleaseComObject(caret);
            }
        }
    }
}
