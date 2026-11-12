using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Islands;

/// <summary>
/// Carries out the islands (M19 B1): Ctrl+Alt+I shows or hides them; Ctrl+Alt+Shift+I starts the
/// edit mode, which shows every monitor at 1× (temporary level), takes clicks, wheel, Esc and Del
/// (<see cref="IIslandEditInput"/>), keeps a drag on its monitor (<see cref="IslandDragFence"/>)
/// and feeds the <see cref="IslandEditor"/>. It ends with Esc, the shortcut again, 2 minutes
/// without input or one of the safety ends of <see cref="IslandEditWatch"/>. Input steps run one at
/// a time under a lock; the islands are saved with the profile.
/// </summary>
public sealed class IslandController : IIslandEditInputHandler, IDisposable
{
    /// <summary>Safety net (plan decision): the edit mode never keeps the clicks longer without input.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(2);

    private readonly Lock _gate = new();
    private readonly IslandModel _model;
    private readonly ZoomModel _zoom;
    private readonly IMonitorProvider _monitors;
    private readonly IIslandEditInput _input;
    private readonly IPointerFence _fence;
    private readonly TimeProvider _time;
    private readonly IslandReport _report;
    private readonly IslandEditWatch _watch;
    private readonly ILogger _log;
    private readonly IslandDragFence _dragFence = new();
    private IslandEditSession? _edit;

    public IslandController(
        SettingsService settings, IMonitorProvider monitors, IIslandEditInput input, IPointerFence fence, IAnnouncer announcer,
        ILocalizer texts, TimeProvider time, ILogger<IslandController> log)
    {
        _model = settings.Models.Islands;
        _zoom = settings.Models.Zoom;
        _monitors = monitors;
        _input = input;
        _fence = fence;
        _time = time;
        _log = log;
        _report = new IslandReport(announcer, texts, log);
        _watch = new IslandEditWatch(fence, _zoom, _model, monitors, () => Volatile.Read(ref _edit)?.Published, End, log);
    }

    public bool IsEditing => Volatile.Read(ref _edit) is not null;

    /// <summary>Ctrl+Alt+I: shows or hides all islands.</summary>
    public void Toggle()
    {
        var visible = !_model.Scene.Visible;
        _model.SetVisible(visible);
        var count = _model.Scene.Set.Count;
        IslandOperation.Toggled(visible, count);
        _log.IslandsToggled(visible, count);
        _report.Say(visible ? "Islands.On" : "Islands.Off");
    }

    /// <summary>Ctrl+Alt+Shift+I or the settings page: starts the edit mode, or ends it if it runs.</summary>
    public void ToggleEdit()
    {
        if (IsEditing)
        {
            End("shortcut");
            return;
        }

        if (Refused() is { } reason)
        {
            _log.EditRefused(reason);
            _report.Say(reason == "docked" ? "Islands.Docked" : "Islands.NoInput");
            return;
        }

        IslandEditSession session;
        try
        {
            // Levels first: their Changed must not find a running edit mode with a monitor still unset.
            foreach (var monitor in _monitors.MonitorIds())
            {
                _zoom.Temporary.Set(TemporaryZoomOwner.IslandEdit, monitor, ZoomLimits.Min);
            }

            var set = _model.Scene.Set.FitTo(_monitors.BoundsOf);
            var idle = _time.CreateTimer(_ => EndLogged("idle"), null, IdleLimit, Timeout.InfiniteTimeSpan);
            session = new IslandEditSession(
                new IslandEditor(set, _monitors, monitor => IslandRules.StartLevel(_zoom, monitor)), Stopwatch.GetTimestamp(), IslandOperation.StartEdit(set.Count), idle);
            lock (_gate)
            {
                _edit = session;
                _model.ShowEdit(set, session.View(), save: false);
            }

            _fence.AddFilter(_dragFence);
            _input.Start(this);
        }
        catch
        {
            // Never leave the monitors at 1× or the clicks taken after a failed start.
            EndLogged("failed");
            _zoom.Temporary.ReleaseAll(TemporaryZoomOwner.IslandEdit);
            throw;
        }

        // Ended on another thread meanwhile (display change, zoom command): undo what came after its end.
        if (!ReferenceEquals(Volatile.Read(ref _edit), session))
        {
            _input.Stop();
            _fence.RemoveFilter(_dragFence);
            return;
        }

        _log.EditStarted(session.Editor.Set.Count);
        _report.Say("Islands.Edit.On");
    }

    /// <summary>Ends the edit mode if it runs; the islands are saved with the profile.</summary>
    public void End(string reason)
    {
        IslandEditSession? edit;
        lock (_gate)
        {
            edit = _edit;
            _edit = null;
            if (edit is null)
            {
                return;
            }

            // A drag in progress is undone, not saved half way; a change from outside (profile switch,
            // settings page) is kept instead of the edit mode's islands.
            edit.Editor.CancelDrag();
            var final = ReferenceEquals(_model.Scene.Set, edit.Published) ? edit.Editor.Set : _model.Scene.Set;
            _model.ShowEdit(final, null, save: true);
        }

        // Clicks go back to applications first, whatever fails afterwards.
        _input.Stop();
        _dragFence.Confine(null);
        try
        {
            _fence.RemoveFilter(_dragFence);
            _zoom.Temporary.ReleaseAll(TemporaryZoomOwner.IslandEdit);
        }
        finally
        {
            edit.Dispose();
            var count = _model.Scene.Set.Count;
            IslandOperation.StopEdit(edit.Operation, reason, count);
            var seconds = Stopwatch.GetElapsedTime(edit.StartedAt).TotalSeconds;
            _log.EditEnded(reason, count, seconds);
            _report.Say("Islands.Edit.Off");
        }
    }

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

    // Docked has no temporary 1× (the strip reserves screen space), so the pointer would not be where
    // the islands are; without the hooks the clicks would reach the applications under the islands.
    private string? Refused() =>
        _zoom.Mode == ZoomMode.Docked ? "docked" : !_input.IsAvailable ? "no_input" : null;

    // One input of the edit mode (capture worker thread). Never throws: the worker goes on.
    private void Step(Func<IslandEditor, IslandEditOutcome> step, string name)
    {
        try
        {
            IslandEditOutcome outcome;
            lock (_gate)
            {
                if (_edit is not { } edit)
                {
                    return; // input queued before the end
                }

                var editor = edit.Editor;
                outcome = step(editor);
                edit.Touch(IdleLimit);
                _dragFence.Confine(editor.DragBounds);
                if (outcome is not (IslandEditOutcome.None or IslandEditOutcome.End))
                {
                    var saved = outcome is IslandEditOutcome.Created or IslandEditOutcome.Changed
                        or IslandEditOutcome.LevelChanged or IslandEditOutcome.Deleted;
                    edit.Published = editor.Set;
                    _model.ShowEdit(editor.Set, edit.View(), saved);
                    _report.Outcome(outcome, editor);
                }
            }

            if (outcome == IslandEditOutcome.End)
            {
                End("escape");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.EditFailed(ex, name);
        }
    }

    private void EndLogged(string reason)
    {
        try
        {
            End(reason);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.EditFailed(ex, "end");
        }
    }
}
