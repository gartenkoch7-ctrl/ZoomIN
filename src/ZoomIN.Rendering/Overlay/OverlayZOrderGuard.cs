using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Overlay;

namespace ZoomIN.Rendering.Overlay;

/// <summary>
/// Puts the overlay back on top whenever another process shows a top-level window
/// (context menus, browser popups, tooltips, an activated always-on-top app). Those are topmost
/// windows created after the overlay and would otherwise cover the zoom unmagnified
/// (docs/kb/capture-ausschluss.md, "Z-Ebenen"). Shell flyouts in higher z-bands need the
/// UIAccess build instead (ADR-0012).
/// </summary>
/// <remarks>
/// Must be created and disposed on the overlay thread: out-of-context WinEvents are delivered
/// through that thread's message loop, so the callback never runs concurrently.
/// </remarks>
internal sealed class OverlayZOrderGuard : IDisposable
{
    private const SET_WINDOW_POS_FLAGS RaiseFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

    // Out-of-context hooks need no DLL and skip our own windows (settings, other overlays),
    // so overlays on several monitors never react to each other.
    private const uint HookFlags = PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS;

    // Kept in a static field so the GC never collects the delegate Windows calls back into.
    private static readonly WINEVENTPROC s_callback = OnWinEvent;

    // One guard per overlay thread; the callback runs on that thread and finds its overlay here.
    [ThreadStatic]
    private static HWND t_overlay;

    private readonly HWINEVENTHOOK _systemHook;
    private readonly HWINEVENTHOOK _showHook;

    public OverlayZOrderGuard(HWND overlay)
    {
        t_overlay = overlay;
        // FOREGROUND..MENUPOPUPSTART also covers MENUSTART/MENUEND, which the policy ignores.
        _systemHook = Hook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_MENUPOPUPSTART);
        _showHook = Hook(PInvoke.EVENT_OBJECT_SHOW, PInvoke.EVENT_OBJECT_SHOW);
    }

    public void Dispose()
    {
        // A failed hook only costs the re-raise, never the zoom itself.
        if (!_systemHook.IsNull)
        {
            PInvoke.UnhookWinEvent(_systemHook);
        }

        if (!_showHook.IsNull)
        {
            PInvoke.UnhookWinEvent(_showHook);
        }

        t_overlay = HWND.Null;
    }

    private static HWINEVENTHOOK Hook(uint eventMin, uint eventMax) =>
        PInvoke.SetWinEventHook(eventMin, eventMax, HMODULE.Null, s_callback, 0, 0, HookFlags);

    private static void OnWinEvent(
        HWINEVENTHOOK hook, uint winEvent, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var overlay = t_overlay;
        if (overlay.IsNull || hwnd.IsNull)
        {
            return;
        }

        var raise = OverlayRaisePolicy.ShouldRaise(
            Classify(winEvent),
            idObject == (int)OBJECT_IDENTIFIER.OBJID_WINDOW,
            PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd,
            PInvoke.IsWindowVisible(overlay));
        if (raise)
        {
            PInvoke.SetWindowPos(overlay, HWND.HWND_TOPMOST, 0, 0, 0, 0, RaiseFlags);
        }
    }

    private static WindowEventKind Classify(uint winEvent) => winEvent switch
    {
        PInvoke.EVENT_SYSTEM_FOREGROUND => WindowEventKind.Foreground,
        PInvoke.EVENT_SYSTEM_MENUPOPUPSTART => WindowEventKind.MenuPopupStart,
        PInvoke.EVENT_OBJECT_SHOW => WindowEventKind.Shown,
        _ => WindowEventKind.Other,
    };
}
