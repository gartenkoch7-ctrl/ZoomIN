using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Input;

/// <summary>
/// The capturing mode of the low-level hooks of <see cref="Win32InputActivityHook"/> for the
/// island edit mode (M19 B1). Only while it runs: mouse buttons, the wheel, Esc and Del are
/// swallowed and handed to the edit mode; pointer moves always go on to Windows. A button or key
/// pressed while capturing keeps its release from applications too, even after the mode ended, so
/// no application sees half a click. <see cref="OnMouse"/> and <see cref="OnKey"/> run on the hook
/// thread: they only queue (Windows removes slow hooks); the handler runs on a worker, in order.
/// </summary>
public sealed class IslandEditCapture : IIslandEditInput
{
    // Window messages of the low-level mouse hook, as the hook passes them.
    internal const uint MouseMove = PInvoke.WM_MOUSEMOVE;
    internal const uint LeftDown = PInvoke.WM_LBUTTONDOWN;
    internal const uint LeftUp = PInvoke.WM_LBUTTONUP;
    internal const uint RightDown = PInvoke.WM_RBUTTONDOWN;
    internal const uint RightUp = PInvoke.WM_RBUTTONUP;
    internal const uint MiddleDown = PInvoke.WM_MBUTTONDOWN;
    internal const uint MiddleUp = PInvoke.WM_MBUTTONUP;
    internal const uint Wheel = PInvoke.WM_MOUSEWHEEL;
    internal const uint ExtraDown = PInvoke.WM_XBUTTONDOWN;
    internal const uint ExtraUp = PInvoke.WM_XBUTTONUP;
    internal const uint HorizontalWheel = PInvoke.WM_MOUSEHWHEEL;

    internal const int EscapeKey = (int)VIRTUAL_KEY.VK_ESCAPE;
    internal const int DeleteKey = (int)VIRTUAL_KEY.VK_DELETE;

    private const double WheelDelta = 120;

    private readonly Func<int, bool> _isDown;
    private readonly Func<bool> _buttonsSwapped;
    private readonly IslandKeyGate _escape = new();
    private readonly IslandKeyGate _delete = new();
    private readonly Func<bool> _takeEscape;
    private readonly Func<bool> _takeDelete;
    private IslandEditQueue? _session;

    // Hook thread only: buttons whose press was swallowed, so their release is too; wheel rest of a run.
    private int _heldButtons;
    private int _wheelRest;
    private IslandEditQueue? _wheelSession;
    private volatile bool _hooksInstalled;
    private volatile uint _primaryDown = LeftDown;
    private volatile uint _primaryUp = LeftUp;

    public IslandEditCapture()
        : this(KeyState.IsDown, () => PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_SWAPBUTTON) != 0)
    {
    }

    /// <param name="isDown">Physical key state by virtual-key code (tests replace it).</param>
    /// <param name="buttonsSwapped">The user swapped the primary and secondary mouse button.</param>
    internal IslandEditCapture(Func<int, bool> isDown, Func<bool>? buttonsSwapped = null)
    {
        _isDown = isDown;
        _buttonsSwapped = buttonsSwapped ?? (() => false);
        _takeEscape = () => Take(IslandEditKey.Escape); // made once: no allocation per key in the hook
        _takeDelete = () => Take(IslandEditKey.Delete);
    }

    public bool IsAvailable => _hooksInstalled;

    /// <summary>Set by <see cref="Win32InputActivityHook"/> once its mouse and keyboard hooks are (no longer) installed.</summary>
    internal void HooksInstalled(bool installed) => _hooksInstalled = installed;

    public void Start(IIslandEditInputHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        // Esc or Del held at the start belongs to whoever got its press: its repeats and release too.
        _escape.Starting(_isDown(EscapeKey));
        _delete.Starting(_isDown(DeleteKey));

        // Low-level hooks report the physical buttons: with swapped buttons (left-handed) the primary
        // one, that drags, arrives as the right button.
        var swapped = _buttonsSwapped();
        _primaryDown = swapped ? RightDown : LeftDown;
        _primaryUp = swapped ? RightUp : LeftUp;
        Interlocked.Exchange(ref _session, new IslandEditQueue(handler))?.Complete();
    }

    public void Stop() => Interlocked.Exchange(ref _session, null)?.Complete();

    /// <summary>Hook thread, every mouse event. Returns true if it must not reach applications.</summary>
    internal bool OnMouse(uint message, PhysicalPoint point, uint mouseData)
    {
        var session = Volatile.Read(ref _session);
        switch (message)
        {
            case MouseMove:
                session?.Move(point);
                return false; // the pointer must move, also while dragging
            case Wheel:
            case HorizontalWheel:
                // Ctrl+wheel and the like stay the zoom's and the applications' (the wheel hook comes after this one).
                if (session is null || AnyModifierDown())
                {
                    return false;
                }

                if (message == Wheel)
                {
                    // Touchpads and fine wheels send parts of a notch: only whole notches change the level
                    // (one step, one save, one announcement). A new run starts from no rest (hook thread only).
                    if (!ReferenceEquals(session, _wheelSession))
                    {
                        _wheelSession = session;
                        _wheelRest = 0;
                    }

                    _wheelRest += (short)(mouseData >> 16);
                    var notches = (int)(_wheelRest / WheelDelta);
                    if (notches != 0)
                    {
                        _wheelRest -= notches * (int)WheelDelta;
                        session.Wheel(notches, point);
                    }
                }

                return true;
            case LeftDown or RightDown or MiddleDown or ExtraDown:
                if (session is null)
                {
                    // A new press: the release of a swallowed one was never seen (secure desktop,
                    // hook timeout); this press's release belongs to the application.
                    _heldButtons &= ~ButtonOf(message, mouseData);
                    return false;
                }

                _heldButtons |= ButtonOf(message, mouseData);
                if (message == _primaryDown)
                {
                    session.Press(point);
                }

                return true;
            case LeftUp or RightUp or MiddleUp or ExtraUp:
                var button = ButtonOf(message, mouseData);
                if ((_heldButtons & button) == 0)
                {
                    return false; // pressed before the edit mode: the application gets its release
                }

                _heldButtons &= ~button;
                if (message == _primaryUp)
                {
                    session?.Release(point);
                }

                return true;
            default:
                return false;
        }
    }

    /// <summary>Hook thread, every key event. Returns true if it must not reach applications.</summary>
    /// <param name="time">Event time in milliseconds (KBDLLHOOKSTRUCT.time, wraps around).</param>
    internal bool OnKey(int virtualKey, bool down, bool up, uint time) => virtualKey switch
    {
        EscapeKey => _escape.OnKey(down, up, time, _takeEscape),
        DeleteKey => _delete.OnKey(down, up, time, _takeDelete),
        _ => false,
    };

    // A new press of Esc or Del. Ctrl+Shift+Esc, Ctrl+Esc, Shift+Del and the like stay the system's.
    private bool Take(IslandEditKey key)
    {
        if (Volatile.Read(ref _session) is not { } session || AnyModifierDown())
        {
            return false;
        }

        session.Key(key);
        return true;
    }

    private bool AnyModifierDown() =>
        _isDown(KeyState.Control) || _isDown(KeyState.Alt) || _isDown(KeyState.Shift)
        || _isDown(KeyState.LeftWindows) || _isDown(KeyState.RightWindows);

    // One bit per button, so a swallowed press never takes the release of another one.
    private static int ButtonOf(uint message, uint mouseData) => message switch
    {
        LeftDown or LeftUp => 1,
        RightDown or RightUp => 2,
        MiddleDown or MiddleUp => 4,
        _ => (mouseData >> 16) == 2 ? 16 : 8, // XBUTTON1 or XBUTTON2 (high word of mouseData)
    };
}
