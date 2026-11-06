using System.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// The work of the filter per window, without thread or hooks (ADR-0020): lists the windows,
/// keeps one state per window, measures new windows once and publishes the regions. Called only
/// from the tracker thread (or a test), so it needs no locks; readers get <see cref="Snapshot"/>.
/// </summary>
internal sealed class WindowTracker : IDisposable
{
    // An unknown window gets new looks when it comes to the front, at most this often.
    private const int MaxRounds = 3;

    private readonly WindowFilterModel _model;
    private readonly IMonitorProvider _monitors;
    private readonly WindowFilterTelemetry _telemetry;
    private readonly ScreenSampler? _sampler;
    private readonly WindowStates _states = new();
    private readonly List<RawWindow> _raw = [];
    private readonly List<TrackedWindow> _tracked = [];
    private readonly List<PhysicalRect> _above = [];
    private readonly List<PhysicalRect> _monitorBounds = [];
    private readonly List<LookRequest> _due = [];
    private TrackerSnapshot _snapshot = TrackerSnapshot.Empty;

    public WindowTracker(WindowFilterModel model, IMonitorProvider monitors, WindowFilterTelemetry telemetry, ScreenSampler? sampler)
    {
        _model = model;
        _monitors = monitors;
        _telemetry = telemetry;
        _sampler = sampler;
    }

    /// <summary>Windows and their palette indexes as last published; read from any thread.</summary>
    public TrackerSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Some window still waits for a look.</summary>
    public bool HasPendingLooks { get; private set; }

    /// <summary>Lists the windows again and publishes the regions.</summary>
    public void Update()
    {
        var started = Stopwatch.GetTimestamp();
        var options = _model.Options;
        if (!options.Enabled)
        {
            HasPendingLooks = false;
            _model.Publish(WindowRegions.Empty);
            Volatile.Write(ref _snapshot, TrackerSnapshot.Empty);
            return;
        }

        WindowEnumerator.Collect(_raw);
        _tracked.Clear();
        var pending = false;
        foreach (var raw in _raw)
        {
            var state = _states.Of(raw);
            if (state.Ignored)
            {
                continue;
            }

            if (!state.Measuring && state.Brightness == WindowBrightness.Pending
                && WindowFilterResolver.NeedsMeasurement(state.Kind, state.Role, options))
            {
                state.StartLooks(started);
            }

            pending |= state.Measuring;
            _tracked.Add(new TrackedWindow(
                raw.Handle, raw.Bounds, state.Kind, state.Role, state.Brightness, raw.Owner, (int)raw.ProcessId,
                state.TitleBarHeightOf(raw), state.TitleBarBrightness));
        }

        _states.EndListing(_raw.Count);
        HasPendingLooks = pending;

        var palette = WindowLayout.Resolve(_tracked, options);
        var regions = WindowLayout.Build(_tracked, palette, TitleBarResolver.Resolve(_tracked, palette, options));
        _model.Publish(regions);
        Volatile.Write(ref _snapshot, new TrackerSnapshot([.. _tracked], palette));
        _telemetry.Updated(Stopwatch.GetElapsedTime(started).TotalMilliseconds, regions);
    }

    /// <summary>Takes the due looks right here (tests, or no look thread); true if a window was decided.</summary>
    public bool Look()
    {
        CollectDueLooks(_due);
        var results = new LookResult[_due.Count];
        for (var i = 0; i < _due.Count; i++)
        {
            results[i] = _due[i].Take(_sampler);
        }

        return Apply(results);
    }

    /// <summary>Fills <paramref name="due"/> with the looks that are due now; true if there are any.</summary>
    public bool CollectDueLooks(List<LookRequest> due)
    {
        due.Clear();
        if (!HasPendingLooks)
        {
            return false;
        }

        var now = Stopwatch.GetTimestamp();
        var threshold = _model.Options.ThresholdPercent;
        CollectMonitors();
        var monitors = _monitorBounds.ToArray();
        var all = Union(monitors);
        _above.Clear();
        foreach (var window in _tracked)
        {
            if (_states.TryGet(window.Handle, out var state) && state.Measuring
                && Stopwatch.GetElapsedTime(state.StartedAt, now) >= state.Vote.NextLookAt)
            {
                // Only the part on some monitor is copied; outside, GDI would deliver black. The
                // title bar is counted apart, so a dark one does not make a light page dark.
                var area = all is { } bounds ? RectMath.Intersection(window.Bounds, bounds) : window.Bounds;
                var titleBarBottom = window.TitleBarHeight > 0 ? window.Bounds.Y + window.TitleBarHeight : double.NegativeInfinity;
                due.Add(new LookRequest(window.Handle, area, [.. _above], monitors, threshold, titleBarBottom));
            }

            _above.Add(window.Bounds);
        }

        return due.Count > 0;
    }

    /// <summary>
    /// Counts the results of looks and decides what is due, also windows past the deadline (with
    /// or without results). True if a window was decided (then call <see cref="Update"/>).
    /// </summary>
    public bool Apply(ReadOnlySpan<LookResult> results)
    {
        foreach (var (handle, isLight, titleBarIsLight) in results)
        {
            if (_states.TryGet(handle, out var state) && state.Measuring)
            {
                state.Vote = state.Vote.Add(isLight);
                if (titleBarIsLight is not null)
                {
                    state.TitleBarVote = state.TitleBarVote.Add(titleBarIsLight);
                }
            }
        }

        // Windows that disappeared while being measured end at the deadline without more looks.
        var now = Stopwatch.GetTimestamp();
        var decided = false;
        var pending = false;
        foreach (var state in _states.All)
        {
            if (state.Measuring)
            {
                decided |= Decide(state, Stopwatch.GetElapsedTime(state.StartedAt, now));
                pending |= state.Measuring;
            }
        }

        HasPendingLooks = pending;
        return decided;
    }

    /// <summary>A window came to the front: if it could not be measured before, it gets another round.</summary>
    public void ForegroundChanged(nint handle)
    {
        if (_states.TryGet(handle, out var state) && state.Brightness == WindowBrightness.Unknown && state.Rounds < MaxRounds)
        {
            state.Brightness = WindowBrightness.Pending;
        }
    }

    public void Dispose() => _sampler?.Dispose();

    private bool Decide(WindowState state, TimeSpan elapsed)
    {
        var result = state.Vote.ResultAfter(elapsed);
        if (result == WindowBrightness.Pending)
        {
            return false;
        }

        state.Finish(result);
        _telemetry.Classified(state.Kind.Process, result, elapsed, state.Vote.Looks, state.TitleBarBrightness, state.TitleBarHeight > 0);
        return true;
    }

    // The box around all monitors; null without monitors (then nothing is clipped).
    private static PhysicalRect? Union(PhysicalRect[] monitors)
    {
        if (monitors.Length == 0)
        {
            return null;
        }

        var left = monitors.Min(m => m.X);
        var top = monitors.Min(m => m.Y);
        return new PhysicalRect(left, top, monitors.Max(m => m.Right) - left, monitors.Max(m => m.Bottom) - top);
    }

    private void CollectMonitors()
    {
        _monitorBounds.Clear();
        foreach (var id in _monitors.MonitorIds())
        {
            if (_monitors.BoundsOf(id) is { } bounds)
            {
                _monitorBounds.Add(bounds);
            }
        }
    }
}
