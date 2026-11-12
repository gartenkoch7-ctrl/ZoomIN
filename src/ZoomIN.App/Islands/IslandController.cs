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
/// without input, a desktop switch, a display change or a zoom command that clears the 1×.
/// Input steps run one at a time under a lock; the island model is saved with the profile.
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
        _fence.Recomputed += OnFenceRecomputed;
        _zoom.Temporary.Changed += OnTemporaryChanged;
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

        if (_zoom.Mode == ZoomMode.Docked)
        {
            // The docked strip has no temporary 1× (it reserves screen space), so the pointer would not be where the islands are.
            _log.EditRefused("docked");
            _report.Say("Islands.Docked");
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

            var set = Refit(_model.Scene.Set);
            var idle = _time.CreateTimer(_ => EndLogged("idle"), null, IdleLimit, Timeout.InfiniteTimeSpan);
            session = new IslandEditSession(
                new IslandEditor(set, _monitors, StartLevel), Stopwatch.GetTimestamp(), IslandOperation.StartEdit(set.Count), idle);
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
            End("failed");
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

            _model.ShowEdit(edit.Editor.Set, null, save: true);
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
            var count = edit.Editor.Set.Count;
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
        _fence.Recomputed -= OnFenceRecomputed;
        _zoom.Temporary.Changed -= OnTemporaryChanged;
        End("exit");
    }

    // One input of the edit mode (capture worker thread). Never throws: the worker goes on.
    private void Step(Func<IslandEditor, IslandEditOutcome> step, string name)
    {
        try
        {
            IslandEditOutcome outcome;
            IslandEditor editor;
            lock (_gate)
            {
                if (_edit is not { } edit)
                {
                    return; // input queued before the end
                }

                editor = edit.Editor;
                outcome = step(editor);
                edit.Touch(IdleLimit);
                _dragFence.Confine(editor.DragBounds);
                if (outcome != IslandEditOutcome.None)
                {
                    var saved = outcome is IslandEditOutcome.Created or IslandEditOutcome.Changed
                        or IslandEditOutcome.LevelChanged or IslandEditOutcome.Deleted;
                    _model.ShowEdit(editor.Set, edit.View(), saved);
                }

                if (outcome != IslandEditOutcome.End)
                {
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

    // A new island starts at the level the monitor shows when zoomed in (plan decision).
    private double StartLevel(string monitorId)
    {
        var level = _zoom.TargetFor(monitorId);
        level = level > ZoomLimits.Min ? level : _zoom.LastLevelFor(ZoomMode.Fullscreen, monitorId);
        return level > ZoomLimits.Min ? level : ZoomLimits.DefaultLevel;
    }

    // The monitors may have changed since the islands were saved: push them back onto their monitor.
    private IslandSet Refit(IslandSet set)
    {
        var fitted = set;
        for (var i = 0; i < set.Count; i++)
        {
            if (_monitors.BoundsOf(set[i].MonitorId) is { } bounds)
            {
                fitted = fitted.Replace(i, IslandRules.Fit(set[i], bounds));
            }
        }

        return fitted;
    }

    // Win+L, UAC, Ctrl+Alt+Del or new monitors: never keep the clicks across them.
    private void OnFenceRecomputed(object? sender, FenceTrigger trigger)
    {
        if (trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock or FenceTrigger.DisplayChange)
        {
            EndLogged(trigger.ToString());
        }
    }

    // A zoom command of the user (zoom off, mode, step to 1×) cleared the edit mode's 1×.
    private void OnTemporaryChanged()
    {
        if (!IsEditing)
        {
            return;
        }

        foreach (var monitor in _monitors.MonitorIds())
        {
            if (_zoom.Temporary.LevelFor(TemporaryZoomOwner.IslandEdit, monitor) is null)
            {
                EndLogged("cleared");
                return;
            }
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
