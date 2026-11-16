namespace ZoomIN.Core.Overlay;

/// <summary>
/// What changed on one monitor since the last presented frame (M14 step 3). With nothing changed
/// the render thread neither draws nor presents: DirectComposition keeps showing the last frame,
/// and a resting zoomed desktop costs almost no GPU time.
/// </summary>
/// <param name="NewDesktopImage">Desktop Duplication delivered a new image.</param>
/// <param name="PointerChanged">Pointer position (on this monitor) or shape changed.</param>
/// <param name="ViewChanged">The magnified part or the lens/strip moved (zoom animation, glide, mode).</param>
/// <param name="RequestChanged">Filter, windows, tracking or pointer options changed (new snapshot).</param>
/// <param name="Animating">Locator ring or smart inversion smoothing still running.</param>
/// <param name="SnapshotRequested">The diagnostics page waits for a debug snapshot.</param>
/// <param name="FirstFrame">Nothing presented yet in this session (or after hiding).</param>
public readonly record struct FrameChanges(
    bool NewDesktopImage,
    bool PointerChanged,
    bool ViewChanged,
    bool RequestChanged,
    bool Animating,
    bool SnapshotRequested,
    bool FirstFrame)
{
    /// <summary>Anything that could make this frame look different from the last one.</summary>
    public bool NeedsRedraw =>
        NewDesktopImage || PointerChanged || ViewChanged || RequestChanged || Animating || SnapshotRequested || FirstFrame;
}
