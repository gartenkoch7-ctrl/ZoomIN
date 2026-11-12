using System.Diagnostics;
using ZoomIN.Core.Islands;

namespace ZoomIN.App.Islands;

/// <summary>
/// One run of the island edit mode (M19 B1): the editor, its start, its operation and its idle timer,
/// which reports this very session (a late timer of an ended run must not end the next one).
/// </summary>
internal sealed class IslandEditSession : IDisposable
{
    private readonly ITimer _idle;
    private readonly TimeSpan _idleLimit;

    public IslandEditSession(IslandEditor editor, Activity? operation, TimeProvider time, TimeSpan idleLimit, Action<IslandEditSession> idle)
    {
        Editor = editor;
        Operation = operation;
        Published = editor.Set;
        StartedAt = Stopwatch.GetTimestamp();
        _idleLimit = idleLimit;
        _idle = time.CreateTimer(_ => idle(this), null, idleLimit, Timeout.InfiniteTimeSpan);
    }

    public IslandEditor Editor { get; }

    /// <summary>Start in <see cref="Stopwatch"/> ticks (the render threads blink from it).</summary>
    public long StartedAt { get; }

    public Activity? Operation { get; }

    /// <summary>The set this edit mode last gave the model; any other set there came from outside.</summary>
    public IslandSet Published { get; set; }

    /// <summary>What the render threads draw for the editor's state now.</summary>
    public IslandEditView View() => new(Editor.Selected, Editor.RubberBand, StartedAt);

    /// <summary>Input came: the safety end waits again for the full idle time.</summary>
    public void Touch() => _idle.Change(_idleLimit, Timeout.InfiniteTimeSpan);

    public void Dispose() => _idle.Dispose();
}
