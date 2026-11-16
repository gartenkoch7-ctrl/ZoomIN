using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Markers;

/// <summary>
/// Zoom off while the hold key is held: after <see cref="MarkerVisibility.MinHoldTime"/> every
/// monitor glides to 1× (temporary level <see cref="TemporaryZoomOwner.HoldKey"/>), so the user can
/// click on the unzoomed desktop; on release it glides back to the zoom from before. Typing with
/// the key held (Shift for a capital letter) is not a hold and changes nothing.
/// </summary>
/// <remarks>
/// The hook thread only re-arms a timer (<see cref="MarkerActivity.HoldChanged"/>); setting and
/// releasing the levels runs on the timer thread. While 1× is held the timer also checks every
/// <see cref="Recheck"/>, so a release the hook missed ends it after
/// <see cref="MarkerVisibility.HoldTimeout"/> at the latest.
/// </remarks>
public sealed class HoldKeyZoom : IDisposable
{
    private static readonly TimeSpan Recheck = TimeSpan.FromMilliseconds(250);

    private readonly SettingsService _settings;
    private readonly IMonitorProvider _monitors;
    private readonly TimeProvider _time;
    private readonly ILogger _log;
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private bool _holding;

    public HoldKeyZoom(SettingsService settings, IMonitorProvider monitors, TimeProvider time, ILogger<HoldKeyZoom> log)
    {
        _settings = settings;
        _monitors = monitors;
        _time = time;
        _log = log;
    }

    private MarkerActivity Activity => _settings.Models.Markers;

    private ZoomModel Zoom => _settings.Models.Zoom;

    public void Start()
    {
        lock (_gate)
        {
            _timer ??= _time.CreateTimer(_ => Check(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        Activity.HoldChanged += OnHoldChanged;
    }

    public void Dispose()
    {
        Activity.HoldChanged -= OnHoldChanged;
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            Release();
        }
    }

    // Hook thread: return at once, the timer does the work. An exception must never escape into
    // the hook callback; a timer disposed meanwhile (shutdown) has nothing left to do.
    private void OnHoldChanged()
    {
        try
        {
            Volatile.Read(ref _timer)?.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>The timer's work; internal for the tests, which fire the timer by hand.</summary>
    internal void Check()
    {
        lock (_gate)
        {
            if (_timer is null)
            {
                return;
            }

            var sample = Activity.Sample;
            var now = _time.GetTimestamp();
            if (MarkerVisibility.IsVisible(ShowCondition.HoldKey, sample, now, _time.TimestampFrequency))
            {
                Hold();
                _timer.Change(Recheck, Timeout.InfiniteTimeSpan);
            }
            else if (sample.HoldSince > 0 && _time.GetElapsedTime(sample.HoldSeen, now) < MarkerVisibility.HoldTimeout)
            {
                // Pressed, but not yet long enough: look again once the minimum time is over.
                var wait = MarkerVisibility.MinHoldTime - _time.GetElapsedTime(sample.HoldSince, now);
                _timer.Change(wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(10), Timeout.InfiniteTimeSpan);
            }
            else
            {
                Release();
            }
        }
    }

    private void Hold()
    {
        // Docked has no temporary levels (the strip reserves screen space): nothing to lift or log.
        if (_holding || Zoom.Mode == ZoomMode.Docked)
        {
            return;
        }

        _holding = true;
        var monitors = _monitors.MonitorIds();
        foreach (var monitor in monitors)
        {
            Zoom.Temporary.Set(TemporaryZoomOwner.HoldKey, monitor, ZoomLimits.Min);
        }

        _log.HoldKeyZoomOff(monitors.Count);
    }

    private void Release()
    {
        if (!_holding)
        {
            return;
        }

        _holding = false;
        Zoom.Temporary.ReleaseAll(TemporaryZoomOwner.HoldKey);
        _log.HoldKeyZoomBack();
    }
}
