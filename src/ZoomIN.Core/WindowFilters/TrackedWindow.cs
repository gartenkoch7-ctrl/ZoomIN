using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>A visible top-level window as the window tracker sees it; listed front to back.</summary>
/// <param name="Handle">Window handle, only used to find owners.</param>
/// <param name="Bounds">Visible frame in physical desktop pixels (without the invisible resize border).</param>
/// <param name="Kind">App + window class.</param>
/// <param name="Role">Normal, popup or Windows interface.</param>
/// <param name="Brightness">Result of the measurement when it opened.</param>
/// <param name="Owner">Owner window handle (GW_OWNER), 0 if none.</param>
/// <param name="ProcessId">Process of the window, to find the app window of a popup without owner.</param>
/// <param name="TitleBarHeight">Height of the title bar strip at the top of <paramref name="Bounds"/> in pixels, 0 if none.</param>
/// <param name="TitleBarBrightness">The title bar measured on its own, in the same looks as the window.</param>
public readonly record struct TrackedWindow(
    nint Handle, PhysicalRect Bounds, WindowKind Kind, WindowRole Role, WindowBrightness Brightness, nint Owner, int ProcessId,
    int TitleBarHeight = 0, WindowBrightness TitleBarBrightness = WindowBrightness.Pending);
