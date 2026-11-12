using System.Diagnostics;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>
/// Colour filter, smoothing and pointer state. Commands come from hotkey and hook threads; render
/// threads read <see cref="CurrentFor"/>, <see cref="LocateRequestedAt"/> and
/// <see cref="PointerHiddenByTyping"/> lock-free.
/// </summary>
public sealed class EffectsModel
{
    private readonly TimeProvider _time;
    private EffectsSnapshot _snapshot;
    private long _locateRequestedAt;
    private volatile bool _typing;

    /// <param name="time">
    /// Clock of the locator request (ADR-0025); the render threads compare it with <see cref="Stopwatch"/>,
    /// so only <see cref="TimeProvider.System"/> or a test clock with the same tick rate fits.
    /// </param>
    public EffectsModel(DisplayEffects initial, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _snapshot = EffectsSnapshot.Create(Normalized(initial));
    }

    /// <summary>Raised after every setting change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised for short-lived events that are not settings (locator, typing); renderers wake up,
    /// nothing is saved. Runs on hook threads: handlers must be fast.
    /// </summary>
    public event Action? Signaled;

    /// <summary>Shared settings; the filter here is the one for monitors without their own.</summary>
    public DisplayEffects Current => Volatile.Read(ref _snapshot).Shared;

    /// <summary><see cref="Stopwatch"/> timestamp of the last locator request; 0 if never.</summary>
    public long LocateRequestedAt => Volatile.Read(ref _locateRequestedAt);

    /// <summary>The user types and "hide while typing" is on; ends with the next mouse movement.</summary>
    public bool PointerHiddenByTyping => _typing;

    /// <summary>Effects as one monitor shows them, with its own filter if it has one. Called per frame, does not allocate.</summary>
    public DisplayEffects CurrentFor(string monitorId) => Volatile.Read(ref _snapshot).For(monitorId);

    /// <summary>Strg+Alt+C: filter of the monitor on/off (the shared one if monitors share it).</summary>
    public void ToggleFilter(string monitorId) => ChangeFilter(monitorId, (_, f) => f with { Enabled = !f.Enabled });

    /// <summary>Strg+Alt+Umschalt+C: next filter on the monitor, own schemes included, and switch it on.</summary>
    public void NextFilter(string monitorId) => ChangeFilter(monitorId, (e, f) => new MonitorFilter(
        true, e.Order[(ColorFilters.IndexIn(e.Order, f.Filter) + 1) % e.Order.Count]));

    /// <summary>
    /// Changes the own schemes (M18; shared by all profiles, cleaned up by <see cref="DisplayEffects.Schemes"/>)
    /// from their current state in one step (add, edit), like <see cref="RemoveScheme"/>. Returning
    /// the list unchanged (e.g. no free place) changes nothing: no new snapshot, no save, no upload.
    /// </summary>
    public void UpdateSchemes(Func<IReadOnlyList<CustomScheme>, IReadOnlyList<CustomScheme>> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Update(e => change(e.Schemes) is var next && ReferenceEquals(next, e.Schemes) ? e : e with { Schemes = next });
    }

    /// <summary>Switches the filter on with <paramref name="filter"/> where the monitor takes it from, like the shortcuts.</summary>
    public void ShowFilter(string monitorId, FilterId filter) => ChangeFilter(monitorId, (_, _) => new MonitorFilter(true, filter));

    /// <summary>
    /// Deletes an own scheme in one step with everything that chose it falling back
    /// (<see cref="SchemeRemoval"/>), so a shortcut at the same moment is not lost. Returns how many fell back.
    /// </summary>
    public int RemoveScheme(FilterId id)
    {
        var fallbacks = 0;
        Update(e =>
        {
            fallbacks = 0;
            return CustomSchemes.Find(e.Schemes, id) is null ? e : SchemeRemoval.Without(e, id, out fallbacks);
        });
        return fallbacks;
    }

    public void SetSmoothing(Smoothing smoothing) => Update(e => e with { Smoothing = smoothing });

    /// <summary>"Schärfe" and "Fett" (M18, D1), clamped to their ranges.</summary>
    public void SetEnhancement(TextEnhancement enhancement) => UpdateEnhancement(_ => enhancement);

    /// <summary>
    /// Changes "Schärfe"/"Fett" from their current values in one step, so a profile switch at the
    /// same moment is not overwritten with old values. The same values again (a slider echo, a drag
    /// past the end) change nothing: no save, no upload.
    /// </summary>
    public void UpdateEnhancement(Func<TextEnhancement, TextEnhancement> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Update(e => change(e.Enhancement).Normalized() is var next && next == e.Enhancement ? e : e with { Enhancement = next });
    }

    /// <summary>Smart inversion: photos and videos stay original (ADR-0024).</summary>
    public void SetProtectPhotos(bool protect) => Update(e => e with { ProtectPhotos = protect });

    /// <summary>Shared filter, contrast and brightness.</summary>
    public void SetFilter(FilterSettings filter) => Update(e => e with { Filter = filter.Normalized() });

    /// <summary>Gives the monitor its own filter; it shows only while <see cref="DisplayEffects.PerMonitorFilter"/> is on.</summary>
    public void SetMonitorFilter(string monitorId, MonitorFilter filter) => Update(e => WithOwn(e, monitorId, filter));

    /// <summary>Off: all monitors show the shared filter; own entries are kept for later.</summary>
    public void SetPerMonitorFilter(bool perMonitor) => Update(e => e with { PerMonitorFilter = perMonitor });

    public void SetCursor(PointerStyle style) => Update(e => e with { Cursor = style.Normalized() });

    /// <summary>Changes the pointer style from its current state in one step (e.g. one field, so Strg+Alt+K is not lost).</summary>
    public void UpdateCursor(Func<PointerStyle, PointerStyle> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Update(e => e with { Cursor = change(e.Cursor).Normalized() });
    }

    /// <summary>Strg+Alt+K: crosshair on/off.</summary>
    public void ToggleCrosshair() => Update(e => e with { Cursor = e.Cursor with { CrosshairEnabled = !e.Cursor.CrosshairEnabled } });

    /// <summary>
    /// Takes over everything at once, e.g. when switching profiles – except the own schemes: they
    /// are shared by all profiles (M18), whatever copy <paramref name="effects"/> carries.
    /// </summary>
    public void Replace(DisplayEffects effects) => Update(e => Normalized(effects) with { Schemes = e.Schemes });

    /// <summary>Shows the pointer locator for a moment (hotkey, shake, …).</summary>
    public void Locate()
    {
        Volatile.Write(ref _locateRequestedAt, _time.GetTimestamp());
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

    private static DisplayEffects Normalized(DisplayEffects e) => e with
    {
        Filter = e.Filter.Normalized(),
        Cursor = e.Cursor.Normalized(),
        Enhancement = e.Enhancement.Normalized(),
        MonitorFilters = e.MonitorFilters ?? new Dictionary<string, MonitorFilter>(),
    };

    // A monitor without an own entry starts from the shared filter and keeps the result as its
    // own entry, like zoom levels (decision: the shared filter stays unchanged).
    private void ChangeFilter(string monitorId, Func<DisplayEffects, MonitorFilter, MonitorFilter> change) => Update(e =>
    {
        var next = change(e, e.FilterOf(monitorId));
        return e.PerMonitorFilter
            ? WithOwn(e, monitorId, next)
            : e with { Filter = e.Filter with { Enabled = next.Enabled, Filter = next.Filter } };
    });

    private static DisplayEffects WithOwn(DisplayEffects e, string monitorId, MonitorFilter filter) =>
        e with { MonitorFilters = new Dictionary<string, MonitorFilter>(e.MonitorFilters) { [monitorId] = filter } };

    private void Update(Func<DisplayEffects, DisplayEffects> change)
    {
        EffectsSnapshot current, next;
        do
        {
            current = Volatile.Read(ref _snapshot);
            var shared = change(current.Shared);

            // A change that returns the same effects is no change: nothing to publish or save.
            if (ReferenceEquals(shared, current.Shared))
            {
                return;
            }

            next = EffectsSnapshot.Create(shared);
        }
        while (Interlocked.CompareExchange(ref _snapshot, next, current) != current);

        Changed?.Invoke();
    }
}
