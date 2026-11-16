using ZoomIN.Core.Modes;

namespace ZoomIN.Core.Filters;

/// <summary>
/// "Schärfe" and "Fett" (M18, D1): magnified text drawn with steeper edges and wider strokes. Both
/// off from the factory and part of the profile. The magnify pass applies them only with
/// text-optimised smoothing and from <see cref="MinZoom"/> on (<see cref="At(double, Smoothing)"/>).
/// </summary>
/// <param name="Sharpness">0 = off (Catmull-Rom as before), 1 = steepest edges (a third of a texel).</param>
/// <param name="BoldPixels">How far strokes grow, in output pixels; 0 = off.</param>
public readonly record struct TextEnhancement(double Sharpness, double BoldPixels)
{
    public const double MaxSharpness = 1;
    public const double MaxBoldPixels = 3;

    /// <summary>
    /// Below this zoom nothing is enhanced: the sharper kernel has no effect at texel centres (1×),
    /// and widening unzoomed text would change the desktop itself (feature plan D1, risk 4).
    /// </summary>
    public const double MinZoom = 1.5;

    // Zoom is a quotient of rectangle sizes; 1.5 must count as 1.5 despite rounding.
    private const double ZoomTolerance = 1e-6;

    public static TextEnhancement Off => default;

    public bool IsActive => Sharpness > 0 || BoldPixels > 0;

    /// <summary>
    /// The sliders act only with text-optimised smoothing: the sharper kernel replaces its bicubic
    /// taps, and "Weich"/"Pixel" were chosen to look soft or blocky (risk 3: no silent switch).
    /// </summary>
    public static bool WorksWith(Smoothing smoothing) => smoothing == Smoothing.Text;

    /// <summary>
    /// Within range and on the steps of the sliders (1 %, 0.1 px), so a value the slider shows as 0 is
    /// off. NaN and infinities count as off.
    /// </summary>
    public TextEnhancement Normalized() =>
        new(Math.Round(Clamp(Sharpness, MaxSharpness), 2), Math.Round(Clamp(BoldPixels, MaxBoldPixels), 1));

    /// <summary>What the shader gets at <paramref name="zoom"/>: these values or <see cref="Off"/>. Per frame, no allocation.</summary>
    public TextEnhancement At(double zoom, Smoothing smoothing) =>
        IsActive && WorksWith(smoothing) && double.IsFinite(zoom) && zoom >= MinZoom - ZoomTolerance ? this : Off;

    /// <summary>
    /// What the shader gets for the drawn view of <paramref name="frame"/>. The axis magnified less
    /// counts: a docked strip can stretch one axis far more than the other. Per frame, no allocation.
    /// </summary>
    public TextEnhancement At(in ModeFrame frame, Smoothing smoothing) =>
        At(Math.Min(frame.Target.Width / frame.Source.Width, frame.Target.Height / frame.Source.Height), smoothing);

    private static double Clamp(double value, double max) => double.IsFinite(value) ? Math.Clamp(value, 0, max) : 0;
}
