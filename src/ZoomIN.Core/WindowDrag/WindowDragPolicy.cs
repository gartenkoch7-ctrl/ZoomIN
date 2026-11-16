using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.WindowDrag;

/// <summary>
/// Which temporary zoom level a window drag puts on the monitor under the pointer (G1). The level
/// is a ceiling (<see cref="TemporaryZoom"/>): 1× lifts the zoom, 2× lowers it only where the user
/// zoomed in further.
/// </summary>
public static class WindowDragPolicy
{
    /// <summary>The level to hold while dragging, or null to leave the zoom alone.</summary>
    /// <remarks>
    /// Docked stays unchanged (decision; the model ignores levels there anyway). The lens hides
    /// while dragging (decision): only 1× lets it glide out, so it gets 1× for "lower" as well.
    /// </remarks>
    public static double? LevelFor(WindowDragKind kind, ZoomMode mode, WindowDragOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Triggers(kind, options) || options.Action == WindowDragAction.Unchanged || mode == ZoomMode.Docked)
        {
            return null;
        }

        return mode == ZoomMode.Lens || options.Action == WindowDragAction.Lift
            ? ZoomLimits.Min
            : options.LowerLevel; // normalized to 1.5–8 by the options
    }

    /// <summary>
    /// True if some drag could change the zoom now (zoom on and a level for moving or resizing).
    /// Derived from <see cref="LevelFor"/>, so both always agree.
    /// </summary>
    public static bool CanAct(ZoomMode mode, bool zoomOn, WindowDragOptions options) =>
        zoomOn && (LevelFor(WindowDragKind.Move, mode, options) is not null || LevelFor(WindowDragKind.Resize, mode, options) is not null);

    /// <summary>
    /// True if this kind of drag is switched on. An unknown kind (the source normally tells it)
    /// counts only if both switches are on.
    /// </summary>
    public static bool Triggers(WindowDragKind kind, WindowDragOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return kind switch
        {
            WindowDragKind.Move => options.OnMove,
            WindowDragKind.Resize => options.OnResize,
            _ => options.OnMove && options.OnResize,
        };
    }
}
