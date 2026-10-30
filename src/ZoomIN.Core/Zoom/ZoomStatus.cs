namespace ZoomIN.Core.Zoom;

/// <summary>Diagnostic state of the zoom output.</summary>
/// <param name="IsVisible">The overlay is shown and rendering.</param>
/// <param name="IsCaptureExcluded">The overlay is excluded from screen capture (WDA_EXCLUDEFROMCAPTURE).</param>
/// <param name="FramesPerSecond">Frames presented during the last second.</param>
/// <param name="LastError">Last failure that stopped or prevented zooming, if any.</param>
public readonly record struct ZoomStatus(
    bool IsVisible,
    bool IsCaptureExcluded,
    int FramesPerSecond,
    string? LastError)
{
    public static ZoomStatus Idle { get; } = new(false, false, 0, null);
}
