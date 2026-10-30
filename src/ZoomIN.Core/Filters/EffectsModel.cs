using System.Diagnostics;

namespace ZoomIN.Core.Filters;

/// <summary>
/// Colour filter, smoothing and pointer state. Commands come from hotkey threads; render threads
/// read <see cref="Current"/> and <see cref="LocateRequestedAt"/> lock-free.
/// </summary>
public sealed class EffectsModel
{
    private static readonly ColorFilter[] s_order = Enum.GetValues<ColorFilter>();

    private DisplayEffects _current;
    private long _locateRequestedAt;

    public EffectsModel(DisplayEffects initial)
    {
        _current = initial with { Filter = initial.Filter.Normalized() };
    }

    /// <summary>Raised after every change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    public DisplayEffects Current => Volatile.Read(ref _current);

    /// <summary><see cref="Stopwatch"/> timestamp of the last Strg+Alt+S; 0 if never.</summary>
    public long LocateRequestedAt => Volatile.Read(ref _locateRequestedAt);

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

    public void SetCursor(CursorStyle cursor) => Update(e => e with { Cursor = cursor });

    /// <summary>Takes over everything at once, e.g. when switching profiles.</summary>
    public void Replace(DisplayEffects effects) => Update(_ => effects with { Filter = effects.Filter.Normalized() });

    /// <summary>Strg+Alt+S: show the pointer locator for a moment.</summary>
    public void Locate()
    {
        Volatile.Write(ref _locateRequestedAt, Stopwatch.GetTimestamp());
        Changed?.Invoke();
    }

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
