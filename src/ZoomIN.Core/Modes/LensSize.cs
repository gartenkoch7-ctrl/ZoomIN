namespace ZoomIN.Core.Modes;

/// <summary>Lens size as fractions of the monitor, so it fits every resolution (M4 decision: 40 % × 30 %).</summary>
public sealed record LensSize(double WidthFraction, double HeightFraction)
{
    public const double MinFraction = 0.1;
    public const double MaxFraction = 0.9;
    public const double Step = 0.05;

    public static LensSize Default { get; } = new(0.4, 0.3);

    /// <summary>Grows or shrinks by whole steps; stays within 10–90 % of the monitor.</summary>
    public LensSize Resize(int widthSteps, int heightSteps) => new(
        Math.Clamp(WidthFraction + (widthSteps * Step), MinFraction, MaxFraction),
        Math.Clamp(HeightFraction + (heightSteps * Step), MinFraction, MaxFraction));
}
