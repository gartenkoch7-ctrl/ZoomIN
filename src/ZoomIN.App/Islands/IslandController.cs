using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Islands;

/// <summary>
/// Carries out the island edit mode (M19 B1; showing and hiding: <see cref="IslandVisibility"/>).
/// Ctrl+Alt+Shift+I starts it: it takes over 1×, the input and the drag filter
/// (<see cref="IslandEditTakeover"/>) and feeds the <see cref="IslandEditor"/>. It ends with Esc,
/// the shortcut again, 2 minutes without input or one of the safety ends of
/// <see cref="IslandEditWatch"/>. Start and end run one at a time (<c>_lifecycle</c>), so an end
/// never gives back what a new start just took; input steps run one at a time under <c>_gate</c>.
/// </summary>
public sealed class IslandController : IIslandEditInputHandler, IDisposable
{
    /// <summary>Safety net (plan decision): the edit mode never keeps the clicks longer without input.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(2);

    private readonly Lock _lifecycle = new();
    private readonly Lock _gate = new();
    private readonly IslandModel _model;
    private readonly ZoomModel _zoom;
    private readonly IMonitorProvider _monitors;
    private readonly IslandEditTakeover _takeover;
    private readonly TimeProvider _time;
    private readonly IslandReport _report;
    private readonly IslandEditWatch _watch;
    private readonly ILogger _log;
    private IslandEditSession? _edit;

    public IslandController(
        SettingsService settings, IMonitorProvider monitors, IIslandEditInput input, IPointerFence fence, IAnnouncer announcer,
        ILocalizer texts, TimeProvider time, ILogger<IslandController> log)
    {
        _model = settings.Models.Islands;
        _zoom = settings.Models.Zoom;
        _monitors = monitors;
        _takeover = new IslandEditTakeover(_zoom, monitors, input, fence);
        _time = time;
        _log = log;
        _report = new IslandReport(announcer, texts, log);
        _watch = new IslandEditWatch(
            fence, _zoom, _model, monitors, () => Volatile.Read(ref _edit) is { } run ? reason => End(reason, run) : null, EndIfChangedOutside, log);

        // The monitors may differ from when the islands were saved: push them back onto their monitor.
        _model.FitTo(monitors.BoundsOf);
    }

    public bool IsEditing => Volatile.Read(ref _edit) is not null;

    /// <summary>Ctrl+Alt+Shift+I or the settings page: starts the edit mode, or ends it if it runs.</summary>
    public void ToggleEdit()
    {
        lock (_lifecycle)
        {
            if (IsEditing)
            {
                End("shortcut");
            }
            else if (_takeover.Refused() is { } reason)
            {
                _log.EditRefused(reason);
                _report.Say(reason == "docked" ? "Islands.Docked" : "Islands.NoInput");
            }
            else
            {
                Start();
            }
        }
    }

    /// <summary>Ends the edit mode if it runs; the islands are saved with the profile.</summary>
    public void End(string reason) => End(reason, only: null);

    public void Pressed(PhysicalPoint point) => Step(editor => editor.Press(point), "press");

    public void Moved(PhysicalPoint point) => Step(editor => editor.Move(point), "move");

    public void Released(PhysicalPoint point) => Step(editor => editor.Release(point), "release");

    public void Wheel(double notches, PhysicalPoint point) => Step(editor => editor.Wheel(notches, point), "wheel");

    public void Key(IslandEditKey key) =>
        Step(editor => key == IslandEditKey.Escape ? editor.Escape() : editor.Delete(), key.ToString());

    public void Dispose()
    {
        _watch.Dispose();
        End("exit");
    }

    // Under _lifecycle.
    private void Start()
    {
        IslandEditSession? session = null;
        var started = false;
        var notStarted = "failed";
        try
        {
            _takeover.ShowOneTimes();
            var current = _model.Scene.Set;
            var set = current.FitTo(_monitors.BoundsOf);
            var editor = new IslandEditor(set, _monitors, monitor => IslandRules.StartLevel(_zoom, monitor));
            session = new IslandEditSession(
                editor, current, IslandOperation.StartEdit(set.Count), _time, IdleLimit, idle => EndLogged("idle", idle));
            lock (_gate)
            {
                _edit = session;
                started = session.Publish(_model, save: false);
                _edit = started ? session : null;
            }

            if (started)
            {
                _takeover.TakeInput(this);
                _log.EditStarted(set.Count);
                _report.Say("Islands.Edit.On");
                return;
            }

            // The islands changed from outside since they were read: start again with the shortcut.
            notStarted = "changed";
            _log.EditRefused(notStarted);
        }
        catch when (started)
        {
            End("failed");
            throw;
        }
        finally
        {
            if (!started)
            {
                // Never leave the monitors at 1× after a start that did not happen.
                session?.Dispose();
                IslandOperation.StopEdit(session?.Operation, notStarted, _model.Scene.Set.Count);
                _takeover.GiveBack();
            }
        }
    }

    // only: end just this run (its idle timer), never a newer one.
    private void End(string reason, IslandEditSession? only)
    {
        lock (_lifecycle)
        {
            IslandEditSession? edit = null;
            try
            {
                lock (_gate)
                {
                    edit = _edit;
                    if (edit is null || (only is not null && !ReferenceEquals(edit, only)))
                    {
                        edit = null;
                        return;
                    }

                    _edit = null;

                    // A drag in progress is undone, not saved half way; a change from outside (profile
                    // switch, settings page) is kept instead of the edit mode's islands.
                    edit.Editor.CancelDrag();
                    _model.EndEdit(edit.Published, edit.Editor.Set);
                }
            }
            finally
            {
                if (edit is not null)
                {
                    try
                    {
                        _takeover.GiveBack();
                    }
                    finally
                    {
                        edit.Dispose();
                        edit.Finish(reason, _model.Scene.Set.Count, _log, _report);
                    }
                }
            }
        }
    }

    // One input of the edit mode (capture worker thread). Never throws: the worker goes on.
    private void Step(Func<IslandEditor, IslandEditOutcome> step, string name)
    {
        try
        {
            string? end;
            IslandEditSession run;
            lock (_gate)
            {
                if (_edit is not { } edit)
                {
                    return; // input queued before the end
                }

                run = edit;
                end = edit.Step(step, _model, _takeover.DragFence, _report);
            }

            if (end is not null)
            {
                End(end, run); // only this run: a new one may have started meanwhile
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.EditFailed(ex, name);
        }
    }

    // The islands changed (any thread). Under the lock, so a step that is publishing is never taken
    // for a change from outside; anything else than the edit mode's own islands ends it.
    private void EndIfChangedOutside()
    {
        IslandEditSession? outside;
        lock (_gate)
        {
            outside = _edit is { } edit && !ReferenceEquals(_model.Scene.Set, edit.Published) ? edit : null;
        }

        if (outside is not null)
        {
            End("changed", outside);
        }
    }

    private void EndLogged(string reason, IslandEditSession? only = null)
    {
        try
        {
            End(reason, only);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.EditFailed(ex, "end");
        }
    }
}
