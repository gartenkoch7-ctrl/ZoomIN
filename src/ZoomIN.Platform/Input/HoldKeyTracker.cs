using System.Diagnostics.Metrics;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Input;

/// <summary>
/// What <see cref="Win32HoldKeyHook"/> knows about the hold key: pressed or not, from key events and
/// from the real key state (E1). Split out of the hook for the file-size rule (M18). Not thread-safe:
/// the hook calls it under its state lock only. No allocation per key.
/// </summary>
internal sealed class HoldKeyTracker
{
    private static readonly Counter<long> s_presses = Telemetry.Meter.CreateCounter<long>(
        "zoomin.highlight.hold_key_presses", "{press}", "Hold key pressed for the highlights (no key content)");

    private bool _down;
    private bool _missedLogged;
    private bool _suspect;
    private bool _releaseSuspect;
    private MarkerHoldKey _downKey;

    /// <summary>A new hook session. A key already held at the start is "down", not a missed press.</summary>
    public void Start(MarkerHoldKey key, bool down)
    {
        _downKey = key;
        // Not counted as a press: the hook restarts on every condition change, also while the key is held.
        _down = down;

        (_suspect, _releaseSuspect) = (false, false);
        _missedLogged = false;
    }

    /// <summary>The hook goes away: a key held now would otherwise count as held for ever.</summary>
    public void Stop()
    {
        _down = false;
        (_suspect, _releaseSuspect) = (false, false);
    }

    /// <summary>One key event from the hook.</summary>
    public void OnKey(IHoldKeyHandler handler, VIRTUAL_KEY key, uint scanCode, bool down, bool up)
    {
        var holdKey = Follow(handler);
        _suspect = false; // the hook is alive: a press it did not report is the lag after a release

        switch (HoldKeyCodes.Classify(key, scanCode, down, up, holdKey))
        {
            case HoldKeyEvent.Down:
                // Key repeat sends further "down" events while held; count the press once. Only events
                // of the hold key itself clear a pending release check, not other keys typed meanwhile.
                _releaseSuspect = false;
                if (!_down)
                {
                    _down = true;
                    s_presses.Add(1);
                }

                handler.HoldKeyDown();
                break;
            case HoldKeyEvent.Up:
                _releaseSuspect = false;
                _down = false;
                handler.HoldKeyUp();
                break;
            case HoldKeyEvent.Other:
                handler.OtherKeyDown();
                break;
        }
    }

    /// <summary>
    /// The real key state, every 400 ms: refreshes a running hold while the key is really down, ends it
    /// otherwise. Returns true the first time a press was missed (to be logged outside the lock).
    /// </summary>
    public bool OnKeyState(IHoldKeyHandler handler, bool pressed)
    {
        Follow(handler);
        var missed = false;
        if (pressed && !_down)
        {
            // Down although the hook saw no press: Windows removed the hook (too slow under load).
            // Only on the second check in a row – right after a release the key state may still say
            // "down", since Windows updates it after the hooks returned.
            if (!_suspect)
            {
                _suspect = true;
                return false;
            }

            missed = !_missedLogged;
            _missedLogged = true;
            _down = true;
            s_presses.Add(1); // a press all the same, only seen late
        }

        _suspect = false;
        if (pressed)
        {
            _releaseSuspect = false;
            handler.HoldKeyDown();
        }
        else if (_down)
        {
            // Up although the hook saw no release: also only on the second check in a row – right
            // after a press the key state may still say "up" for the same reason.
            if (!_releaseSuspect)
            {
                _releaseSuspect = true;
                return missed;
            }

            _releaseSuspect = false;
            _down = false;
            handler.HoldKeyUp();
        }

        return missed;
    }

    // A new hold key (for key events and key state checks alike): the old one's release will not
    // come, so its press no longer counts as the new key being down.
    private MarkerHoldKey Follow(IHoldKeyHandler handler)
    {
        var holdKey = handler.Key;
        if (holdKey != _downKey)
        {
            (_down, _downKey, _suspect, _releaseSuspect) = (false, holdKey, false, false);
        }

        return holdKey;
    }
}
