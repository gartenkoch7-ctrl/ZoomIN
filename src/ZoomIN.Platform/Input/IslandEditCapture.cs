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
    // Window messages of the low-level mouse hook.
    internal const uint MouseMove = 0x0200;
    internal const uint LeftDown = 0x0201;
    internal const uint LeftUp = 0x0202;
    internal const uint RightDown = 0x0204;
    internal const uint RightUp = 0x0205;
    internal const uint MiddleDown = 0x0207;
    internal const uint MiddleUp = 0x0208;
    internal const uint Wheel = 0x020A;
    internal const uint ExtraDown = 0x020B;
    internal const uint ExtraUp = 0x020C;
    internal const uint HorizontalWheel = 0x020E;

    internal const int EscapeKey = 0x1B;
    internal const int DeleteKey = 0x2E;

    private const double WheelDelta = 120;
    private const uint RepeatGapMilliseconds = 2500;

    private IslandEditQueue? _session;

    // Hook thread only: buttons and keys whose press was swallowed, so their release is too.
    private int _heldButtons;
    private bool _escapeHeld;
    private bool _deleteHeld;
    private uint _escapeAt;
    private uint _deleteAt;
    private volatile bool _hooksInstalled;

    /// <summary>True while the edit mode takes the input.</summary>
    public bool IsCapturing => Volatile.Read(ref _session) is not null;

    public bool IsAvailable => _hooksInstalled;

    /// <summary>Set by <see cref="Win32InputActivityHook"/> once its mouse and keyboard hooks are (no longer) installed.</summary>
    internal void HooksInstalled(bool installed) => _hooksInstalled = installed;

    public void Start(IIslandEditInputHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Interlocked.Exchange(ref _session, new IslandEditQueue(handler))?.Complete();
    }

    public void Stop() => Interlocked.Exchange(ref _session, null)?.Complete();

    /// <summary>Hook thread, every mouse event. Returns true if it must not reach applications.</summary>
    internal bool OnMouse(uint message, int x, int y, uint mouseData)
    {
        var session = Volatile.Read(ref _session);
        var point = new PhysicalPoint(x, y);
        switch (message)
        {
            case MouseMove:
                session?.Move(x, y);
                return false; // the pointer must move, also while dragging
            case Wheel:
            case HorizontalWheel:
                if (session is null)
                {
                    return false;
                }

                if (message == Wheel)
                {
                    session.Wheel((short)(mouseData >> 16) / WheelDelta, point);
                }

                return true;
            case LeftDown or RightDown or MiddleDown or ExtraDown:
                if (session is null)
                {
                    // A new press: the release of a swallowed one was never seen (secure desktop,
                    // hook timeout); this press's release belongs to the application.
                    _heldButtons &= ~ButtonOf(message);
                    return false;
                }

                _heldButtons |= ButtonOf(message);
                if (message == LeftDown)
                {
                    session.Press(point);
                }

                return true;
            case LeftUp or RightUp or MiddleUp or ExtraUp:
                var button = ButtonOf(message);
                if ((_heldButtons & button) == 0)
                {
                    return false; // pressed before the edit mode: the application gets its release
                }

                _heldButtons &= ~button;
                if (message == LeftUp)
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
    internal bool OnKey(int virtualKey, bool down, bool up, uint time)
    {
        if (virtualKey is not (EscapeKey or DeleteKey))
        {
            return false;
        }

        ref var held = ref virtualKey == EscapeKey ? ref _escapeHeld : ref _deleteHeld;
        ref var last = ref virtualKey == EscapeKey ? ref _escapeAt : ref _deleteAt;

        // Key repeat starts within 1 s (2 s with FilterKeys): a "repeat" later than that is a new
        // press whose release before it was never seen (secure desktop, hook timeout).
        if (down && held && time - last > RepeatGapMilliseconds)
        {
            held = false;
        }

        last = time;
        if (up)
        {
            var swallow = held;
            held = false;
            return swallow;
        }

        if (!down)
        {
            return false;
        }

        // Key repeat of a swallowed press: swallowed, but not handed on again.
        if (held)
        {
            return true;
        }

        if (Volatile.Read(ref _session) is not { } session)
        {
            return false;
        }

        held = true;
        session.Key(virtualKey == EscapeKey ? IslandEditKey.Escape : IslandEditKey.Delete);
        return true;
    }

    // Extra buttons share one bit: which of the two hardly matters for keeping the release.
    private static int ButtonOf(uint message) => message switch
    {
        LeftDown or LeftUp => 1,
        RightDown or RightUp => 2,
        MiddleDown or MiddleUp => 4,
        _ => 8,
    };
}
