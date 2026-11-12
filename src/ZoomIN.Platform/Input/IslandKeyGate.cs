namespace ZoomIN.Platform.Input;

/// <summary>
/// One key the island edit mode takes (Esc or Del, M19 B1), on the hook thread: a swallowed press
/// keeps its repeats and release swallowed; a press already held when the edit mode started keeps
/// them for the application. Key repeat starts within 1 s (2 s with FilterKeys), so a "repeat" after
/// <see cref="RepeatGapMilliseconds"/> is a new press whose release was lost (secure desktop, hook timeout).
/// </summary>
internal sealed class IslandKeyGate
{
    public const uint RepeatGapMilliseconds = 2500;

    // Hook thread only.
    private bool _held;
    private uint _last;
    private uint _passAt;

    // Set by Start on another thread; cleared only here.
    private volatile bool _pass;

    /// <summary>The edit mode starts: a press held now belongs to whoever got it.</summary>
    public void Starting(bool down) => _pass = down;

    /// <summary>Hook thread, every event of this key.</summary>
    /// <param name="take">Hands a new press to the edit mode; false if it does not take it (not running, modifier held).</param>
    /// <returns>True if the event must not reach applications.</returns>
    public bool OnKey(bool down, bool up, uint time, Func<bool> take)
    {
        if (_pass)
        {
            // Unless ZoomIN swallowed that press itself (held): then its release stays swallowed.
            var stale = down && _passAt != 0 && time - _passAt > RepeatGapMilliseconds;
            if (up || _held || stale)
            {
                _passAt = 0;
                _pass = false;
            }

            if (!_held && !stale)
            {
                _passAt = up ? 0 : time;
                _last = time;
                return false;
            }
        }

        if (down && _held && time - _last > RepeatGapMilliseconds)
        {
            _held = false;
        }

        _last = time;
        if (up)
        {
            var swallow = _held;
            _held = false;
            return swallow;
        }

        if (!down)
        {
            return false;
        }

        // Key repeat of a swallowed press: swallowed, but not handed on again.
        if (_held)
        {
            return true;
        }

        _held = take();
        return _held;
    }
}
