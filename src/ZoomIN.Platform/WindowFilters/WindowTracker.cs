using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
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
    private readonly WindowNames _names = new();
    private readonly Dictionary<nint, WindowState> _states = [];
    private readonly List<RawWindow> _raw = [];
    private readonly List<TrackedWindow> _tracked = [];
    private readonly List<PhysicalRect> _above = [];
    private readonly List<PhysicalRect> _monitorBounds = [];
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
        var processes = new HashSet<uint>();
        var pending = false;
        foreach (var raw in _raw)
        {
            var state = StateOf(raw);
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
            processes.Add(raw.ProcessId);
            _tracked.Add(new TrackedWindow(raw.Handle, raw.Bounds, state.Kind, state.Role, state.Brightness, raw.Owner, (int)raw.ProcessId));
        }

        ForgetClosedWindows();
        _names.KeepOnly(processes);
        HasPendingLooks = pending;

        var palette = WindowLayout.Resolve(_tracked, options);
        var regions = WindowLayout.Build(_tracked, palette);
        _model.Publish(regions);
        Volatile.Write(ref _snapshot, new TrackerSnapshot([.. _tracked], palette));
        WindowFilterTelemetry.Updated(Stopwatch.GetElapsedTime(started).TotalMilliseconds, regions.Count);
    }

    /// <summary>Takes the looks that are due; true if a window was decided (then call <see cref="Update"/>).</summary>
    public bool Look()
    {
        if (!HasPendingLooks)
        {
            return false;
        }

        var now = Stopwatch.GetTimestamp();
        var threshold = _model.Options.ThresholdPercent;
        var decided = false;
        CollectMonitors();
        _above.Clear();
        foreach (var window in _tracked)
        {
            if (_states.TryGetValue(window.Handle, out var state) && state.Measuring)
            {
                var elapsed = Stopwatch.GetElapsedTime(state.StartedAt, now);
                if (elapsed >= state.Vote.NextLookAt)
                {
                    state.Vote = state.Vote.Add(LookAt(window.Bounds, threshold));
                }

                decided |= Decide(state, elapsed);
            }

            _above.Add(window.Bounds);
        }

        // Windows that disappeared while being measured end at the deadline without more looks.
        foreach (var state in _states.Values)
        {
            if (state.Measuring)
            {
                decided |= Decide(state, Stopwatch.GetElapsedTime(state.StartedAt, now));
            }
        }

        HasPendingLooks = _states.Values.Any(s => s.Measuring);
        return decided;
    }

    /// <summary>A window came to the front: if it could not be measured before, it gets another round.</summary>
    public void ForegroundChanged(nint handle)
    {
        if (_states.TryGetValue(handle, out var state) && state.Brightness == WindowBrightness.Unknown && state.Rounds < MaxRounds)
        {
            state.Brightness = WindowBrightness.Pending;
        }
    }

    public void Dispose() => _sampler?.Dispose();

    private WindowState StateOf(in RawWindow raw)
    {
        if (!_states.TryGetValue(raw.Handle, out var state))
        {
            var kind = _names.KindOf(raw.Handle, raw.ProcessId);
            state = new WindowState(kind, WindowClasses.RoleOf(kind, raw.PopupStyle, raw.Owner != 0, raw.ToolWindow), WindowClasses.IsIgnored(kind.WindowClass));
            _states[raw.Handle] = state;
        }

        return state;
    }

    private bool Decide(WindowState state, TimeSpan elapsed)
    {
        var result = state.Vote.ResultAfter(elapsed);
        if (result == WindowBrightness.Pending)
        {
            return false;
        }

        state.Finish(result);
        _telemetry.Classified(state.Kind.Process, result, elapsed, state.Vote.Looks);
        return true;
    }

    private bool? LookAt(PhysicalRect bounds, int threshold)
    {
        if (_sampler is null)
        {
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        var area = ClipToMonitors(bounds);
        bool? result = null;
        if (!RectMath.IsEmpty(area) && _sampler.Capture(area))
        {
            result = BrightnessMeter.IsLight(
                _sampler.Pixels, ScreenSampler.Size, ScreenSampler.Size, area,
                _above.ToArray(), _monitorBounds.ToArray(), threshold);
        }

        WindowFilterTelemetry.Looked(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }

    // Only the part on some monitor is copied; outside, GDI would deliver black.
    private PhysicalRect ClipToMonitors(PhysicalRect bounds)
    {
        if (_monitorBounds.Count == 0)
        {
            return bounds;
        }

        var left = _monitorBounds.Min(m => m.X);
        var top = _monitorBounds.Min(m => m.Y);
        var all = new PhysicalRect(left, top, _monitorBounds.Max(m => m.Right) - left, _monitorBounds.Max(m => m.Bottom) - top);
        return RectMath.Intersection(bounds, all);
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

    // Hidden windows keep their state (no new looks when shown again); closed ones are dropped.
    private void ForgetClosedWindows()
    {
        if (_states.Count <= _raw.Count)
        {
            return;
        }

        foreach (var handle in _states.Keys.Where(h => !PInvoke.IsWindow((HWND)h)).ToList())
        {
            _states.Remove(handle);
        }
    }
}
