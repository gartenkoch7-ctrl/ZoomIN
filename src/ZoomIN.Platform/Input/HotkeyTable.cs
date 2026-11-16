using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>
/// The shortcuts of <see cref="Win32HotkeyService"/>, used only on its thread (thread-bound hotkeys).
/// RegisterHotKey is per session, not per desktop (M10 test 2026-11-16): while the tray app holds a
/// shortcut, the secure-desktop instance cannot have it. So the tray app gives its shortcuts up while
/// another desktop has the input, and a refused shortcut is tried again for a few seconds.
/// </summary>
internal sealed class HotkeyTable(ILogger log)
{
    /// <summary>Retries every <see cref="RetryIntervalMs"/>: the other instance gives up within about a second.</summary>
    public const uint RetryIntervalMs = 250;
    public const int MaxRetries = 20;

    private readonly Dictionary<int, HotkeyEntry> _active = [];
    private readonly List<HotkeyEntry> _waiting = [];
    private int _nextId = 1;

    /// <summary>Another desktop has the input; nothing is registered with Windows meanwhile.</summary>
    public bool Suspended { get; private set; }

    /// <summary>Some shortcut waits for a retry.</summary>
    public bool NeedsRetry => !Suspended && _waiting.Count > 0;

    /// <summary>
    /// The caller keeps a timer running: for retries, and while suspended to look at the input
    /// desktop again, so a missed switch event never leaves the tray app without shortcuts.
    /// </summary>
    public bool NeedsTimer => Suspended || _waiting.Count > 0;

    /// <returns>False if another application holds the gesture; it is tried again for a while.</returns>
    public bool Add(HotkeyGesture gesture, Action onPressed, bool repeat)
    {
        var entry = new HotkeyEntry(_nextId++, gesture, onPressed, repeat);
        if (Suspended)
        {
            // Registered when this desktop has the input again.
            _waiting.Add(entry);
            return true;
        }

        if (TryRegister(entry))
        {
            return true;
        }

        log.HotkeyInUse(gesture);
        _waiting.Add(entry);
        return false;
    }

    public Action? Callback(int id) => _active.TryGetValue(id, out var entry) ? entry.OnPressed : null;

    public void Retry()
    {
        if (Suspended)
        {
            return;
        }

        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            var entry = _waiting[i];
            entry.Attempts++;
            if (TryRegister(entry))
            {
                _waiting.RemoveAt(i);
                log.HotkeyRegisteredLate(entry.Gesture, entry.Attempts);
            }
            else if (entry.Attempts >= MaxRetries && !entry.Owned)
            {
                // Really held by another application: stays unavailable, as reported by Add.
                _waiting.RemoveAt(i);
            }
            else if (entry.Attempts == MaxRetries)
            {
                // Ours before the desktop switch (the secure instance may linger): keep trying, but say so once.
                log.HotkeyInUse(entry.Gesture);
            }
        }
    }

    /// <summary>Gives every shortcut up while another desktop has the input, and takes them back after.</summary>
    /// <param name="ownDesktopHasInput">Null when unknown: then nothing changes.</param>
    public void DesktopChanged(bool? ownDesktopHasInput)
    {
        if (ownDesktopHasInput == false && !Suspended)
        {
            Suspended = true;
            foreach (var entry in _active.Values)
            {
                PInvoke.UnregisterHotKey(HWND.Null, entry.Id);
                entry.Attempts = 0;
                _waiting.Add(entry);
            }

            _active.Clear();
            log.HotkeysDesktopSwitch("released", _waiting.Count);
        }
        else if (ownDesktopHasInput == true && Suspended)
        {
            Suspended = false;
            var count = _waiting.Count;
            for (var i = _waiting.Count - 1; i >= 0; i--)
            {
                _waiting[i].Attempts = 0;
                if (TryRegister(_waiting[i]))
                {
                    _waiting.RemoveAt(i);
                }
            }

            log.HotkeysDesktopSwitch("taken back", count - _waiting.Count);
        }
    }

    public void Clear()
    {
        foreach (var id in _active.Keys)
        {
            PInvoke.UnregisterHotKey(HWND.Null, id);
        }

        _active.Clear();
        _waiting.Clear();
    }

    private bool TryRegister(HotkeyEntry entry)
    {
        var modifiers = (HOT_KEY_MODIFIERS)entry.Gesture.Modifiers;
        if (!entry.Repeat)
        {
            modifiers |= HOT_KEY_MODIFIERS.MOD_NOREPEAT;
        }

        if (!PInvoke.RegisterHotKey(HWND.Null, entry.Id, modifiers, (uint)entry.Gesture.VirtualKey))
        {
            return false;
        }

        _active[entry.Id] = entry;
        entry.Owned = true;
        return true;
    }

    private sealed class HotkeyEntry(int id, HotkeyGesture gesture, Action onPressed, bool repeat)
    {
        public int Id { get; } = id;
        public HotkeyGesture Gesture { get; } = gesture;
        public Action OnPressed { get; } = onPressed;
        public bool Repeat { get; } = repeat;
        public int Attempts { get; set; }

        /// <summary>Was registered once: a refusal later comes from our own other instance, not a foreign app.</summary>
        public bool Owned { get; set; }
    }
}
