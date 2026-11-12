using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Overview;

namespace ZoomIN.Platform.Input;

/// <summary>
/// The overview's keys (B2) on the low-level keyboard hook of <see cref="Win32InputActivityHook"/>:
/// RegisterHotKey reports only the press, so the release of the shortcut comes from here, and Esc
/// is swallowed only while an overview runs. <see cref="OnKey"/> runs on the hook thread for every
/// key: no allocation, no lock, no log.
/// </summary>
public sealed class OverviewKeyWatch : IOverviewKeys
{
    private const int Escape = 0x1B;

    // Key repeat starts at most 1 s after the press (2 s with FilterKeys). An Esc "repeat" later
    // than this belongs to a new press: the release of the old one was missed.
    private const uint RepeatGapMilliseconds = 2500;

    private static readonly TimeSpan RecheckDelay = TimeSpan.FromMilliseconds(100);

    private readonly ILogger _log;
    private readonly Func<int, bool> _isDown;
    private readonly TimeProvider _time;
    private ActiveWatch? _watch;

    // Esc went down while swallowing: its repeats and its release are swallowed too, also after
    // the overview ended, so the application never sees half a key press.
    private volatile bool _escapeDown;
    private uint _lastEscapeAt;

    // This Esc press belongs to someone else – it was held when the overview started, or it was a
    // Windows shortcut (Win+Esc, Ctrl+Shift+Esc): its repeats and release go on to them as well.
    private volatile bool _passEscape;
    private int _escapesSwallowed;

    public OverviewKeyWatch(ILogger<OverviewKeyWatch> log, TimeProvider time)
        : this(log, KeyState.IsDown, time)
    {
    }

    /// <param name="isDown">Physical key state by virtual-key code (tests replace it).</param>
    internal OverviewKeyWatch(ILogger log, Func<int, bool> isDown, TimeProvider time)
    {
        _log = log;
        _isDown = isDown;
        _time = time;
    }

    public void Watch(long watchId, HotkeyGesture gesture, IOverviewKeyHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var watch = new ActiveWatch(watchId, gesture, handler);
        _passEscape = _isDown(Escape) && !_escapeDown;
        Volatile.Write(ref _watch, watch);
        Interlocked.Exchange(ref _escapesSwallowed, 0);

        // The release may have come before the watch started (a very short tap). Windows updates
        // the async key state only after the hook returns, so a release passing the hook right
        // now still reads as down: look once more a moment later.
        CheckReleased(watch);
        _ = Task.Delay(RecheckDelay, _time).ContinueWith(static (_, state) => ((Recheck)state!).Run(), new Recheck(this, watch), TaskScheduler.Default);
    }

    private void CheckReleased(ActiveWatch watch)
    {
        if (!_isDown(watch.Gesture.VirtualKey) || !ModifiersDown(watch.Gesture.Modifiers))
        {
            watch.Released(late: true);
        }
    }

    public void Stop(long watchId)
    {
        var watch = Volatile.Read(ref _watch);
        if (watch is null || watch.Id != watchId || Interlocked.CompareExchange(ref _watch, null, watch) != watch)
        {
            return;
        }

        // Counted on the hook thread, logged here: never a log per key, and no key of the user.
        var swallowed = Interlocked.Exchange(ref _escapesSwallowed, 0);
        try
        {
            if (swallowed > 0)
            {
                _log.OverviewEscapesSwallowed(swallowed);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The watch is already gone; a failing log must not keep the overview from ending.
        }
    }

    /// <summary>Hook thread, every key event. Returns true if the key must not reach applications.</summary>
    /// <param name="time">Event time in milliseconds (KBDLLHOOKSTRUCT.time, wraps around).</param>
    internal bool OnKey(int virtualKey, bool down, bool up, uint time)
    {
        var watch = Volatile.Read(ref _watch);
        if (virtualKey == Escape)
        {
            // A missed release (desktop switch, hook timeout) must not swallow later presses.
            // Only on a press: a long-held Esc without key repeat must keep its release swallowed.
            if (down && time - _lastEscapeAt > RepeatGapMilliseconds)
            {
                _escapeDown = false;
                _passEscape = false;
            }

            _lastEscapeAt = time;
            if (_passEscape)
            {
                _passEscape = !up;
                return false;
            }

            // Windows' own Esc shortcuts pass (see WindowsEscape).
            if (down && !_escapeDown && watch is not null && WindowsEscape(watch.Gesture))
            {
                _passEscape = true; // its repeats and release go to Windows as well
                return false;
            }

            if (down && (watch is not null || _escapeDown))
            {
                if (!_escapeDown && watch is not null)
                {
                    Interlocked.Increment(ref _escapesSwallowed);
                    watch.Escape();
                }

                _escapeDown = true;
                return true;
            }

            if (up && _escapeDown)
            {
                _escapeDown = false;
                return true;
            }

            return false;
        }

        if (up && watch is not null && Belongs(virtualKey, watch.Gesture))
        {
            watch.Released(late: false);
        }

        return false;
    }

    // Win+Esc and Ctrl+Shift+Esc (Task Manager) belong to Windows, unless those keys are part of the
    // shortcut or its main key is still held. Ctrl or Alt alone stay the overview's: they are what
    // is left of the shortcut after tapping its main key (Ctrl+Esc, Alt+Esc wait for its end).
    private bool WindowsEscape(HotkeyGesture gesture)
    {
        if (_isDown(gesture.VirtualKey))
        {
            return false;
        }

        var own = gesture.Modifiers;
        var win = (_isDown(KeyState.LeftWindows) || _isDown(KeyState.RightWindows)) && !own.HasFlag(HotkeyModifiers.Windows);
        var taskManager = _isDown(KeyState.Control) && _isDown(KeyState.Shift) && !_isDown(KeyState.Alt)
            && !own.HasFlag(HotkeyModifiers.Shift);
        return win || taskManager;
    }

    private bool ModifiersDown(HotkeyModifiers modifiers) =>
        (!modifiers.HasFlag(HotkeyModifiers.Control) || _isDown(KeyState.Control))
        && (!modifiers.HasFlag(HotkeyModifiers.Alt) || _isDown(KeyState.Alt))
        && (!modifiers.HasFlag(HotkeyModifiers.Shift) || _isDown(KeyState.Shift))
        && (!modifiers.HasFlag(HotkeyModifiers.Windows) || _isDown(KeyState.LeftWindows) || _isDown(KeyState.RightWindows));

    // The low-level hook reports left/right variants of the modifiers.
    private static bool Belongs(int key, HotkeyGesture gesture)
    {
        if (key == gesture.VirtualKey)
        {
            return true;
        }

        var modifier = KeyState.ModifierOf(key);
        return modifier != HotkeyModifiers.None && gesture.Modifiers.HasFlag(modifier);
    }

    /// <summary>The delayed second look of <see cref="Watch"/>, only while that watch still runs.</summary>
    private sealed class Recheck(OverviewKeyWatch owner, ActiveWatch watch)
    {
        public void Run()
        {
            if (ReferenceEquals(Volatile.Read(ref owner._watch), watch))
            {
                owner.CheckReleased(watch);
            }
        }
    }

    /// <summary>One overview's watch; reports its release once.</summary>
    private sealed class ActiveWatch(long id, HotkeyGesture gesture, IOverviewKeyHandler handler)
    {
        private int _released;

        public long Id => id;

        public HotkeyGesture Gesture => gesture;

        public void Released(bool late)
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                handler.ShortcutReleased(id, late);
            }
        }

        // An Esc settles the watch: no release may follow it (from the hook or the second look).
        public void Escape()
        {
            Interlocked.Exchange(ref _released, 1);
            handler.EscapePressed(id);
        }
    }
}
