using System.Diagnostics;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>
/// Colour filter, smoothing and pointer state. Commands come from hotkey and hook threads; render
/// threads read <see cref="Current"/>, <see cref="LocateRequestedAt"/> and
/// <see cref="PointerHiddenByTyping"/> lock-free.
/// </summary>
public sealed class EffectsModel
{
    private static readonly ColorFilter[] s_order = Enum.GetValues<ColorFilter>();

    private DisplayEffects _current;
    private long _locateRequestedAt;
    private volatile bool _typing;

    public EffectsModel(DisplayEffects initial)
    {
        _current = Normalized(initial);
    }

    /// <summary>Raised after every setting change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised for short-lived events that are not settings (locator, typing); renderers wake up,
    /// nothing is saved. Runs on hook threads: handlers must be fast.
    /// </summary>
    public event Action? Signaled;

    public DisplayEffects Current => Volatile.Read(ref _current);

    /// <summary><see cref="Stopwatch"/> timestamp of the last locator request; 0 if never.</summary>
    public long LocateRequestedAt => Volatile.Read(ref _locateRequestedAt);

    /// <summary>The user types and "hide while typing" is on; ends with the next mouse movement.</summary>
    public bool PointerHiddenByTyping => _typing;

    /// <summary>Strg+Alt+C: selected filter on/off.</summary>
    public void ToggleFilter() => Update(e => e with { Filter = e.Filter with { Enabled = !e.Filter.Enabled } });

    /// <summary>Strg+Alt+Umschalt+C: next filter, and switch it on.</summary>
    public void NextFilter() => Update(e =>
    {
        var next = s_order[(Array.IndexOf(s_order, e.Filter.Filter) + 1) % s_order.Length];
        return e with { Filter = e.Filter with { Filter = next, Enabled = true } };
    });

    public void SetSmoothing(Smoothing smoothing) => Update(e => e with { Smoothing = smoothing });

    public void SetFilter(FilterSettings filter) => Update(e => e with { Filter = filter.Normalized() });

    public void SetCursor(PointerStyle style) => Update(e => e with { Cursor = style.Normalized() });

    /// <summary>Strg+Alt+K: crosshair on/off.</summary>
    public void ToggleCrosshair() => Update(e => e with { Cursor = e.Cursor with { CrosshairEnabled = !e.Cursor.CrosshairEnabled } });

    /// <summary>Takes over everything at once, e.g. when switching profiles.</summary>
    public void Replace(DisplayEffects effects) => Update(_ => Normalized(effects));

    /// <summary>Shows the pointer locator for a moment (hotkey, shake, …).</summary>
    public void Locate()
    {
        Volatile.Write(ref _locateRequestedAt, Stopwatch.GetTimestamp());
        Signaled?.Invoke();
    }

    /// <summary>A key was typed: hides the pointer if that option is on.</summary>
    public void MarkTyped()
    {
        if (!_typing && Current.Cursor.HideWhileTyping)
        {
            _typing = true;
            Signaled?.Invoke();
        }
    }

    /// <summary>The mouse moved: the pointer shows again.</summary>
    public void MarkMoved()
    {
        if (_typing)
        {
            _typing = false;
            Signaled?.Invoke();
        }
    }

    private static DisplayEffects Normalized(DisplayEffects e) =>
        e with { Filter = e.Filter.Normalized(), Cursor = e.Cursor.Normalized() };

    private void Update(Func<DisplayEffects, DisplayEffects> change)
    {
        DisplayEffects current, next;
        do
        {
            current = Volatile.Read(ref _current);
            next = change(current);
        }
        while (Interlocked.CompareExchange(ref _current, next, current) != current);

        Changed?.Invoke();
    }
}
