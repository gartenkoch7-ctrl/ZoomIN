using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Islands;

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

    /// <param name="published">The islands in the model when the run starts (the editor starts with them fitted to the monitors).</param>
    public IslandEditSession(
        IslandEditor editor, IslandSet published, Activity? operation, TimeProvider time, TimeSpan idleLimit, Action<IslandEditSession> idle)
    {
        Editor = editor;
        Operation = operation;
        Published = published;
        _time = time;
        StartedAt = time.GetTimestamp();
        _idleLimit = idleLimit;
        _idle = time.CreateTimer(_ => idle(this), null, idleLimit, Timeout.InfiniteTimeSpan);
    }

    public IslandEditor Editor { get; }

    /// <summary>
    /// Start in <see cref="TimeProvider.GetTimestamp"/> ticks; the render threads blink from it with
    /// <see cref="Stopwatch"/>, which the system time provider is based on.
    /// </summary>
    public long StartedAt { get; }

    public Activity? Operation { get; }

    /// <summary>The set this edit mode last gave the model; any other set there came from outside.</summary>
    public IslandSet Published { get; private set; }

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
        fence.Confine(Editor.DragBounds);
        if (outcome == IslandEditOutcome.End)
        {
            return "escape";
        }

        // Also after "None": the editor's islands or selection may still differ from what is drawn
        // (a drag released where it began, a selection at the wheel's limit). Unchanged = no event.
        var saved = outcome is IslandEditOutcome.Created or IslandEditOutcome.Changed or IslandEditOutcome.LevelChanged or IslandEditOutcome.Deleted;
        if (!Publish(model, saved))
        {
            return "changed";
        }

        report.Outcome(outcome, Editor);
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
        if (model.ShowEdit(before, Editor.Set, View(), save))
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
