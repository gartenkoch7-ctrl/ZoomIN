namespace ZoomIN.Core.Zoom;

/// <summary>Zoom range and step size (M2 decisions: 1×–16×, one step = ×1.25).</summary>
public static class ZoomLimits
{
    public const double Min = 1.0;
    public const double Max = 16.0;
    public const double StepFactor = 1.25;
    public const double DefaultLevel = 2.0;

    public static double Clamp(double zoom) => Math.Clamp(zoom, Min, Max);

    /// <summary>
    /// Multiplicative steps feel equally large at every level. Fractional notches come from
    /// precision touchpads and high-resolution wheels.
    /// </summary>
    public static double Step(double zoom, double notches) => Clamp(zoom * Math.Pow(StepFactor, notches));
}
