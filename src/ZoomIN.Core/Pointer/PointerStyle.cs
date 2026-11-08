namespace ZoomIN.Core.Pointer;

/// <summary>
/// Everything about the magnified pointer (pointer page): size with and without zoom, colour,
/// contrast rim, locator, crosshair and hiding while typing.
/// </summary>
public sealed record PointerStyle
{
    public static PointerStyle Default { get; } = new();

    /// <summary>While zoomed the pointer grows with the zoom up to this factor.</summary>
    public double MaxScale { get; init; } = 4.0;

    /// <summary>Pointer size without zoom; 1 = Windows draws its own pointer.</summary>
    public double UnzoomedScale { get; init; } = 1.0;

    /// <summary>
    /// ZoomIN ignores the Windows pointer size and colour (accessibility settings, schemes) and
    /// always draws the standard Windows pointers in its own size and colour, also without zoom.
    /// </summary>
    public bool OverrideWindows { get; init; }

    /// <summary>True: the size is multiplied by the zoom (up to <see cref="MaxScale"/>). False: fixed size.</summary>
    public bool GrowWithZoom { get; init; } = true;

    public PointerColorMode ColorMode { get; init; } = PointerColorMode.Original;

    public RgbColor Color { get; init; } = RgbColor.Yellow;

    /// <summary>Adaptive mode (M15): body colour on dark ground; the outline there takes the other colour.</summary>
    public RgbColor AdaptiveOnDark { get; init; } = RgbColor.White;

    /// <summary>Adaptive mode (M15): body colour on light ground.</summary>
    public RgbColor AdaptiveOnLight { get; init; } = RgbColor.Black;

    /// <summary>Adaptive mode: background brightness (0–1) from which the ground counts as light.</summary>
    public double AdaptiveThreshold { get; init; } = 0.5;

    public bool Rim { get; init; } = true;

    public RgbColor RimColor { get; init; } = RgbColor.Amber;

    /// <summary>Rim width in screen pixels.</summary>
    public double RimWidth { get; init; } = 2;

    public RgbColor LocatorColor { get; init; } = RgbColor.Amber;

    /// <summary>Start radius of the locator ring in pixels.</summary>
    public double LocatorSize { get; init; } = 150;

    public double LocatorSeconds { get; init; } = 1.0;

    public LocatorTriggers LocatorTriggers { get; init; } = LocatorTriggers.Hotkey | LocatorTriggers.Shake | LocatorTriggers.AfterIdle;

    public bool CrosshairEnabled { get; init; }

    /// <summary>False: only while zoomed.</summary>
    public bool CrosshairAlways { get; init; }

    public RgbColor CrosshairColor { get; init; } = RgbColor.Cyan;

    public double CrosshairWidth { get; init; } = 2;

    public double CrosshairOpacity { get; init; } = 0.8;

    public bool HideWhileTyping { get; init; }

    /// <summary>
    /// Pointer scale: the unzoomed size times the zoom, capped at <see cref="MaxScale"/> but never
    /// below the unzoomed size; without <see cref="GrowWithZoom"/> always the unzoomed size.
    /// </summary>
    public double ScaleFor(double zoom)
    {
        var size = Math.Max(1.0, UnzoomedScale);
        return GrowWithZoom ? Math.Min(size * Math.Max(1.0, zoom), Math.Max(size, MaxScale)) : size;
    }

    /// <summary>
    /// Size a standard pointer is loaded at for drawing at <paramref name="scale"/>: the next power
    /// of two (1, 2, 4, 8), so a zoom animation needs at most four images per pointer and none is
    /// shrunk by more than half (bilinear sampling stays smooth).
    /// </summary>
    public static int SharpScale(double scale)
    {
        var sharp = 1;
        while (sharp < 8 && sharp < scale - 0.001)
        {
            sharp *= 2;
        }

        return sharp;
    }

    /// <summary>ZoomIN draws the pointer itself (instead of Windows).</summary>
    public bool DrawsPointer(double zoom) =>
        zoom > 1.01 || UnzoomedScale > 1.01 || ColorMode != PointerColorMode.Original || OverrideWindows;

    public bool CrosshairVisible(double zoom) => CrosshairEnabled && (CrosshairAlways || zoom > 1.01);

    /// <summary>
    /// The pointer needs the desktop under it, also without zoom: the adaptive pointer reads what is
    /// shown below (ADR-0026), so Desktop Duplication runs on the monitor with the mouse.
    /// </summary>
    public bool NeedsDesktop => ColorMode == PointerColorMode.Adaptive;

    /// <summary>Without zoom an overlay is still needed for the pointer or the crosshair.</summary>
    public bool NeedsOverlayUnzoomed => DrawsPointer(1.0) || CrosshairVisible(1.0);

    /// <summary>Rim width in screen pixels for a pointer scale: grows with large pointers.</summary>
    public double RimPixelsFor(double scale) => Rim ? Math.Max(RimWidth, RimWidth * scale / 2) : 0;

    /// <summary>Keeps hand-edited values in sensible ranges.</summary>
    public PointerStyle Normalized() => this with
    {
        MaxScale = Math.Clamp(MaxScale, 1, 8),
        UnzoomedScale = Math.Clamp(UnzoomedScale, 1, 4),
        RimWidth = Math.Clamp(RimWidth, 1, 8),
        AdaptiveThreshold = Math.Clamp(AdaptiveThreshold, 0.1, 0.9),
        ColorMode = Enum.IsDefined(ColorMode) ? ColorMode : PointerColorMode.Original,
        LocatorSize = Math.Clamp(LocatorSize, 60, 400),
        LocatorSeconds = Math.Clamp(LocatorSeconds, 0.3, 3),
        CrosshairWidth = Math.Clamp(CrosshairWidth, 1, 10),
        CrosshairOpacity = Math.Clamp(CrosshairOpacity, 0.2, 1),
    };
}
