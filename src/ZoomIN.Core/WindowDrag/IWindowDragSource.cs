namespace ZoomIN.Core.WindowDrag;

/// <summary>
/// Reports when a top-level window is moved or resized by the user (G1). Implemented in Platform
/// with out-of-context WinEvents; never reports window titles.
/// </summary>
public interface IWindowDragSource : IDisposable
{
    /// <summary>Starts watching. <paramref name="signal"/> runs on the source's own thread, one call at a time.</summary>
    /// <param name="signal">Receives the drag steps.</param>
    /// <param name="active">Asked at each move/size start; false = leave the window alone (no probing).</param>
    void Start(Action<WindowDragSignal> signal, Func<bool> active);
}
