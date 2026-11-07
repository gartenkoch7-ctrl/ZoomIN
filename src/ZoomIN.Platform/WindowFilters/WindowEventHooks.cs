using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// Out-of-context WinEvent hooks of the window filter thread (ADR-0020): show, hide, move, Z order,
/// foreground and cloak of top-level windows. Created and disposed on that thread, whose message
/// loop delivers the callbacks. The static callback only forwards to the instance (ADR-0025).
/// </summary>
internal sealed class WindowEventHooks : IDisposable
{
    // Event ranges: foreground; move/size; minimize; destroy–reorder; location; cloak/uncloak.
    private static readonly (uint Min, uint Max)[] s_events =
        [(0x0003, 0x0003), (0x000A, 0x000B), (0x0016, 0x0017), (0x8001, 0x8004), (0x800B, 0x800B), (0x8017, 0x8018)];

    private const uint EventForeground = 0x0003;
    private const uint EventDestroy = 0x8001;

    // One filter thread per process; the static callback finds its hooks here.
    private static WindowEventHooks? s_current;
    private static readonly WINEVENTPROC s_callback = OnEvent;

    private readonly List<HWINEVENTHOOK> _hooks = [];
    private readonly Action<nint> _foreground;
    private readonly Action _changed;

    /// <param name="foreground">A window came to the foreground; called before <paramref name="changed"/>.</param>
    /// <param name="changed">Any tracked window changed.</param>
    public WindowEventHooks(Action<nint> foreground, Action changed)
    {
        _foreground = foreground;
        _changed = changed;
        s_current = this;
        foreach (var (min, max) in s_events)
        {
            var hook = PInvoke.SetWinEventHook(min, max, HMODULE.Null, s_callback, 0, 0, 0); // WINEVENT_OUTOFCONTEXT
            if (hook != HWINEVENTHOOK.Null)
            {
                _hooks.Add(hook);
            }
        }
    }

    /// <summary>How many of the event ranges are hooked; 0 means the filter sees no changes.</summary>
    public int Count => _hooks.Count;

    public void Dispose()
    {
        foreach (var hook in _hooks)
        {
            PInvoke.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        s_current = null;
    }

    private static void OnEvent(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Only whole top-level windows; carets and controls report location changes too. A
        // destroyed window has no ancestor any more, so destroy events skip that check.
        if (s_current is not { } hooks || hwnd == HWND.Null || idObject != 0 || idChild != 0
            || (@event != EventDestroy && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd))
        {
            return;
        }

        if (@event == EventForeground)
        {
            hooks._foreground(hwnd);
        }

        hooks._changed();
    }
}
