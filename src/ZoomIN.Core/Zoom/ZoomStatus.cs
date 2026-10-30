namespace ZoomIN.Core.Zoom;

/// <summary>Diagnostic state of the zoom output.</summary>
/// <param name="IsVisible">An overlay is shown and rendering.</param>
/// <param name="IsCaptureExcluded">The overlays are excluded from screen capture (WDA_EXCLUDEFROMCAPTURE).</param>
/// <param name="FramesPerSecond">Frames presented during the last second (slowest visible monitor).</param>
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
