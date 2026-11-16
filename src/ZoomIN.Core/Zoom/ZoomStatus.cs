namespace ZoomIN.Core.Zoom;

/// <summary>Diagnostic state of the zoom output.</summary>
/// <param name="IsVisible">An overlay is shown and rendering.</param>
/// <param name="IsCaptureExcluded">The overlays are excluded from screen capture (WDA_EXCLUDEFROMCAPTURE).</param>
/// <param name="FramesPerSecond">
/// Render passes on time during the last second (slowest visible monitor): presented frames plus
/// idle passes, where nothing changed and the last frame stays on screen (M14). It shows whether
/// the zoom keeps up with the refresh rate, not how much was drawn.
/// </param>
/// <param name="LastError">Last failure that stopped or prevented zooming, if any.</param>
public readonly record struct ZoomStatus(
    bool IsVisible,
    bool IsCaptureExcluded,
    int FramesPerSecond,
    string? LastError)
{
    public static ZoomStatus Idle { get; } = new(false, false, 0, null);

    /// <summary>Number of monitors with a zoom pipeline.</summary>
    public int MonitorCount { get; init; }
}
