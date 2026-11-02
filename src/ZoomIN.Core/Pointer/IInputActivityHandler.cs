namespace ZoomIN.Core.Pointer;

/// <summary>Receives input activity on the hook thread; every method must return quickly.</summary>
public interface IInputActivityHandler
{
    /// <param name="timestamp"><see cref="System.Diagnostics.Stopwatch"/> timestamp.</param>
    /// <param name="x">Screen position in physical pixels.</param>
    /// <param name="y">Screen position in physical pixels.</param>
    void MouseMoved(long timestamp, int x, int y);

    /// <summary>A character key was pressed without Ctrl, Alt or Windows.</summary>
    void Typed();

    /// <summary>Ctrl was pressed and released on its own.</summary>
    void CtrlTapped();
}
