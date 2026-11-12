using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Islands;

/// <summary>
/// One run of the island edit mode (M19 B1): the editor, its start, its operation and its idle timer,
/// which reports this very session (a late timer of an ended run must not end the next one).
/// </summary>
internal sealed class IslandEditSession : IDisposable
{
    private readonly TimeProvider _time;
    private readonly ITimer _idle;
    private readonly TimeSpan _idleLimit;
    private Core.Viewport.PhysicalRect? _confined;

    /// <param name="published">The islands in the model when the run starts (the editor starts with them fitted to the monitors).</param>
    public IslandEditSession(
        IslandEditor editor, IslandSet published, IslandSet saved, Activity? operation, TimeProvider time, TimeSpan idleLimit,
        Action<IslandEditSession> idle)
    {
        Editor = editor;
        Operation = operation;
        Published = published;
        Fitted = editor.Set;
        Saved = saved;
        _time = time;
        StartedAt = time.GetTimestamp();
        _idleLimit = idleLimit;
        _idle = time.CreateTimer(_ => idle(this), null, idleLimit, Timeout.InfiniteTimeSpan);
    }

    public IslandEditor Editor { get; }

    /// <summary>A new run on the islands as drawn now, fitted to the monitors; its operation starts with it.</summary>
    public static IslandEditSession Create(
        IslandModel model, IMonitorProvider monitors, ZoomModel zoom, TimeProvider time, TimeSpan idleLimit, Action<IslandEditSession> idle)
    {
        var current = model.Scene.Set;
        var set = current.FitTo(monitors.BoundsOf);
        var editor = new IslandEditor(set, monitors, monitor => IslandRules.StartLevel(zoom, monitor));
        var operation = IslandOperation.StartEdit(set.Count);
        try
        {
            return new IslandEditSession(editor, current, model.Committed, operation, time, idleLimit, idle);
        }
        catch
        {
            IslandOperation.StopEdit(operation, "failed", set.Count); // never a started operation left open
            throw;
        }
    }

    /// <summary>
    /// Start in <see cref="TimeProvider.GetTimestamp"/> ticks; the render threads blink from it with
    /// <see cref="Stopwatch"/>, which the system time provider is based on.
    /// </summary>
    public long StartedAt { get; }

    public Activity? Operation { get; }

    /// <summary>The set this edit mode last gave the model; any other set there came from outside.</summary>
    public IslandSet Published { get; private set; }

    /// <summary>The islands at the start, fitted to the monitors; drawn after a run that changed nothing (and not saved).</summary>
    public IslandSet Fitted { get; }

    /// <summary>A step finished a change (created, moved, resized, level, deleted).</summary>
    public bool HasChanges { get; private set; }

    /// <summary>What the run leaves behind: its islands, or the fitted ones of its start if it changed nothing.</summary>
    public IslandSet Result => HasChanges ? Editor.Set : Fitted;

    /// <summary>The saved islands at the start: islands the run did not touch are saved as they were.</summary>
    public IslandSet Saved { get; }

    /// <summary>What is saved for <see cref="Result"/>: edited islands as edited, the others as saved before.</summary>
    public IslandSet Commit => Result.WithUntouchedFrom(Fitted, Saved);

    /// <summary>What the render threads draw for the editor's state now.</summary>
    public IslandEditView View() => new(Editor.Selected, Editor.RubberBand, StartedAt, Editor.DragBounds is not null);

    /// <summary>Input came: the safety end waits again for the full idle time.</summary>
    public void Touch() => _idle.Change(_idleLimit, Timeout.InfiniteTimeSpan);

    /// <summary>One input (under the controller's lock): editor, idle time, drag fence, model, announcement.</summary>
    /// <returns>Why the edit mode must end now (<c>escape</c>, <c>changed</c>), or null.</returns>
    public string? Step(Func<IslandEditor, IslandEditOutcome> step, IslandModel model, IslandDragFence fence, IslandReport report)
    {
        var outcome = step(Editor);
        Touch();
        if (Editor.DragBounds != _confined)
        {
            _confined = Editor.DragBounds; // only at the start and end of a drag, not per move
            fence.Confine(_confined);
        }
        if (outcome == IslandEditOutcome.End)
        {
            return "escape";
        }

        // Also after "None": the editor's islands or selection may still differ from what is drawn
        // (a drag released where it began, a selection at the wheel's limit). Unchanged = no event.
        var saved = outcome is IslandEditOutcome.Created or IslandEditOutcome.Changed or IslandEditOutcome.LevelChanged or IslandEditOutcome.Deleted;
        HasChanges |= saved;
        if (!Publish(model, saved))
        {
            return "changed";
        }

        report.Outcome(outcome, Editor);

        // A new island is meant to be seen: hidden islands (Ctrl+Alt+I) come back on.
        if (outcome == IslandEditOutcome.Created && !model.Scene.Visible)
        {
            model.SetVisible(true);
        }

        return null;
    }

    /// <summary>
    /// Gives the editor's state to the model unless the islands changed from outside meanwhile.
    /// <see cref="Published"/> is set first: the model's Changed of this very step comes back on this
    /// thread and must find it.
    /// </summary>
    /// <returns>False if the islands changed from outside; nothing was published.</returns>
    public bool Publish(IslandModel model, bool save)
    {
        var before = Published;
        Published = Editor.Set;
        if (model.ShowEdit(before, Editor.Set, View(), save, save ? Editor.Set.WithUntouchedFrom(Fitted, Saved) : null))
        {
            return true;
        }

        Published = before;
        return false;
    }

    /// <summary>The run is over: operation, log and announcement.</summary>
    public void Finish(string reason, int count, ILogger log, IslandReport report)
    {
        IslandOperation.StopEdit(Operation, reason, count);
        var seconds = _time.GetElapsedTime(StartedAt).TotalSeconds;
        log.EditEnded(reason, count, seconds);
        report.Say("Islands.Edit.Off");
    }

    public void Dispose() => _idle.Dispose();
}
