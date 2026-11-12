using System.Diagnostics;
using ZoomIN.Core.Islands;

namespace ZoomIN.App.Islands;

/// <summary>One run of the island edit mode (M19 B1): the editor, its start, its operation and the idle timer.</summary>
internal sealed class IslandEditSession(IslandEditor editor, long startedAt, Activity? operation, ITimer idle) : IDisposable
{
    public IslandEditor Editor => editor;

    /// <summary>Start in <see cref="Stopwatch"/> ticks (the render threads blink from it).</summary>
    public long StartedAt => startedAt;

    public Activity? Operation => operation;

    /// <summary>The set this edit mode last gave the model; any other set there came from outside.</summary>
    public IslandSet Published { get; set; } = editor.Set;

    /// <summary>What the render threads draw for the editor's state now.</summary>
    public IslandEditView View() => new(editor.Selected, editor.RubberBand, startedAt);

    /// <summary>Input came: the safety end waits again for the full idle time.</summary>
    public void Touch(TimeSpan idleLimit) => idle.Change(idleLimit, Timeout.InfiniteTimeSpan);

    public void Dispose() => idle.Dispose();
}
