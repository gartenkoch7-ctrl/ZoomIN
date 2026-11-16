using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Overview;

/// <summary>
/// Carries out the overview (B2): the shortcut and the hook signals go to <see cref="OverviewModel"/>,
/// whose start and end this turns into the temporary 1×, Esc handling, the view request for zooming
/// in or going back, the announcement and the operation <c>zoomin.view.overview</c>. Handlers run
/// one at a time under a lock; the hook's signals go through <see cref="OverviewKeySignals"/>.
/// </summary>
public sealed class OverviewController : IDisposable
{
    private readonly Lock _gate = new();
    // TrySet and ReleaseUnlessNewer are never called while holding _gate (they raise Changed into
    // OnTemporaryChanged, which takes it). IsHeld only reads the model and is safe under _gate.
    private readonly OverviewLevel _level;
    private readonly SettingsService _settings;
    private readonly ZoomModel _zoom;
    private readonly OverviewModel _overview;
    private readonly ZoomCommands _commands;
    private readonly OverviewFrameSource _frames;
    private readonly IOverviewKeys _keys;
    private readonly IAnnouncer _announcer;
    private readonly ILocalizer _texts;
    private readonly OverviewKeySignals _signals;
    private readonly ILogger _log;

    // Overview whose start was carried out (level set, keys watched): only it can be "cleared by a command".
    private long _activeId;

    // Per started overview (an older end may come after the next start): operation, "on" announced (pairs).
    private readonly Dictionary<long, (Activity? Operation, bool Announced)> _started = [];

    public OverviewController(
        SettingsService settings, ZoomCommands commands, IMonitorProvider monitors, ICursorSource cursor, IOverviewKeys keys,
        IAnnouncer announcer, ILocalizer texts, TimeProvider time, ILogger<OverviewController> log)
    {
        _settings = settings;
        _zoom = settings.Models.Zoom;
        _overview = settings.Models.Overview;
        _level = new OverviewLevel(_zoom, _overview);
        _commands = commands;
        _frames = new OverviewFrameSource(
            monitors, cursor, _zoom, settings.Models.Tracking, settings.Models.ViewRequests, time, log);
        _keys = keys;
        _announcer = announcer;
        _texts = texts;
        _signals = new OverviewKeySignals(_overview, time, log);
        _log = log;
        _overview.Started += OnStarted;
        _overview.Ended += OnEnded;
        _zoom.Temporary.Changed += OnTemporaryChanged;
    }

    /// <summary>The shortcut went down (hotkey thread). Returns the overview it started, if any.</summary>
    public OverviewSession? Press()
    {
        var monitor = _commands.MonitorUnderMouse();
        return _overview.Press(monitor, _zoom.Mode, _zoom.TargetFor(monitor));
    }

    public void Dispose()
    {
        _overview.Started -= OnStarted;
        _overview.Ended -= OnEnded;
        _zoom.Temporary.Changed -= OnTemporaryChanged;
    }

    private void OnStarted(OverviewSession session)
    {
        try
        {
            if (SetUp(session))
            {
                _overview.Cancel(session);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Half set up (1× without Esc or release): end it rather than leave the user stuck.
            _log.OverviewFailed(ex, "set_up");
            CancelLogged(session);
        }
    }

    // Returns true if the overview is already lost and must be cancelled.
    private bool SetUp(OverviewSession session)
    {
        // Ended on another thread before this handler ran: nothing to set up.
        if (!_level.TrySet(session))
        {
            return false;
        }

        bool ended;
        var lost = false;
        lock (_gate)
        {
            // Ended while setting the level: its end may have released before the Set.
            ended = !ReferenceEquals(_overview.Current, session);
            if (!ended)
            {
                _activeId = session.Id;
                _keys.Watch(session.Id, _settings.Current.OverviewHotkey, _signals);

                // A zoom command between the press and Set clears levels before this one was set
                // (set-after-clear): then the overview would show 1× on a monitor the user switched.
                lost = !_level.IsHeld(session.MonitorId)
                    || _zoom.Mode != session.Mode || _zoom.TargetFor(session.MonitorId) <= ZoomLimits.Min;
                _log.OverviewStarted(session.MonitorId, session.LevelBefore);

                // Under the lock (the announcer never waits), so "off" can never come before "on".
                _started[session.Id] = (OverviewOperation.Start(session), !lost);
                if (!lost)
                {
                    _announcer.Announce(_texts.Get("ViewOverview.On"));
                }
            }
        }

        if (ended)
        {
            _level.ReleaseUnlessNewer(session);
        }

        return lost;
    }

    private void OnEnded(OverviewEnded ended)
    {
        var session = ended.Session;
        try
        {
            Finish(ended);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Other Ended handlers (the render wake-up) must still run.
            _log.OverviewFailed(ex, "end");
        }
        finally
        {
            // Whatever failed above, the monitor must not stay at 1×, and other Ended handlers run.
            try
            {
                _level.ReleaseUnlessNewer(session);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.OverviewFailed(ex, "release");
            }
        }
    }

    private void Finish(OverviewEnded ended)
    {
        var session = ended.Session;
        lock (_gate)
        {
            if (_activeId == session.Id)
            {
                _activeId = 0;
            }

            try
            {
                _keys.Stop(session.Id); // Esc goes back to applications at once; never throws
                _frames.PlaceView(ended); // never throws; before the zoom glides back: the anchor needs the point
            }
            finally
            {
                // Ended before its start was carried out (another thread): nothing was shown or announced.
                if (_started.Remove(session.Id, out var started))
                {
                    OverviewOperation.Stop(started.Operation, ended);
                    _log.OverviewEnded(ended.End, ended.Held);
                    if (started.Announced)
                    {
                        _announcer.Announce(_texts.Get("ViewOverview.Off"));
                    }
                }
            }
        }
    }

    // A user command cleared the level (zoom off, mode change, step to 1×): the overview is over.
    private void OnTemporaryChanged()
    {
        OverviewSession? cleared = null;
        lock (_gate)
        {
            if (_overview.Current is { } session && _activeId == session.Id && !_level.IsHeld(session.MonitorId))
            {
                cleared = session;
            }
        }

        // Raised on the thread of the zoom command (also the UI thread): never let it fail there.
        if (cleared is not null)
        {
            CancelLogged(cleared);
        }
    }

    private void CancelLogged(OverviewSession session)
    {
        try
        {
            _overview.Cancel(session);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.OverviewFailed(ex, "cancel");
        }
    }

    /// <summary>The frame of the overview with the mouse now, or null if the monitor or mouse is unknown.</summary>
    internal PhysicalRect? FrameNow(OverviewSession session) => _frames.Now(session);
}
