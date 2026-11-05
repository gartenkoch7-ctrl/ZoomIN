using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// Chromium (Chrome, Edge, Electron apps such as VS Code and Teams) builds its accessibility tree
/// only once it believes a screen reader or magnifier is running; until then focus and caret of
/// web content are unknown. Asking a content window for IAccessible2 is what such tools do and
/// switches it on for that browser process only – unlike the system-wide screen reader flag,
/// which would change the behavior of every application.
/// </summary>
internal sealed unsafe class ChromiumAccessibility
{
    private const string ContentClass = "Chrome_RenderWidgetHostHWND";
    private const string FrameClass = "Chrome_WidgetWin_1";

    private static readonly Guid IAccessibleId = new("618736E0-3C3D-11CF-810C-00AA00389B71");
    private static readonly Guid IAccessible2Id = new("E89F726E-C4F4-4C19-BB19-B647D7FA8478");

    private HWND _lastTouched;

    /// <summary>True when the window belongs to a Chromium frame.</summary>
    public static bool IsChromium(HWND window) => ContentWindow(window) is { IsNull: false };

    /// <summary>
    /// Switches on accessibility for the Chromium process owning <paramref name="window"/>, once per
    /// content window. Returns true when the window is Chromium content and was asked.
    /// </summary>
    public bool Touch(HWND window)
    {
        var content = ContentWindow(window);
        if (content.IsNull)
        {
            return false;
        }

        if (content == _lastTouched)
        {
            return true;
        }

        _lastTouched = content;
        RequestIAccessible2(content);
        return true;
    }

    private static HWND ContentWindow(HWND window)
    {
        if (window.IsNull)
        {
            return HWND.Null;
        }

        if (ClassOf(window) == ContentClass)
        {
            return window;
        }

        var root = PInvoke.GetAncestor(window, GET_ANCESTOR_FLAGS.GA_ROOT);
        return !root.IsNull && ClassOf(root) == FrameClass
            ? PInvoke.FindWindowEx(root, HWND.Null, ContentClass, null)
            : HWND.Null;
    }

    private static string ClassOf(HWND window)
    {
        Span<char> name = stackalloc char[64];
        var length = PInvoke.GetClassName(window, name);
        return length > 0 ? new string(name[..length]) : string.Empty;
    }

    private static void RequestIAccessible2(HWND content)
    {
        IAccessible? accessible = null;
        try
        {
            if (PInvoke.AccessibleObjectFromWindow(content, unchecked((uint)OBJECT_IDENTIFIER.OBJID_CLIENT), out accessible).Failed
                || accessible is not Windows.Win32.System.Com.IServiceProvider services)
            {
                return;
            }

            var service = IAccessibleId;
            var interfaceId = IAccessible2Id;
            services.QueryService(&service, &interfaceId, out var result);
            if (result is not null)
            {
                Marshal.Release((nint)result);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            // Older Chromium without IAccessible2 or a window that just closed: nothing to switch on.
        }
        finally
        {
            if (accessible is not null)
            {
                Marshal.ReleaseComObject(accessible);
            }
        }
    }
}
