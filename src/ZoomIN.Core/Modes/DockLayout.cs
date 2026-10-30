namespace ZoomIN.Core.Modes;

/// <summary>Docked strip: edge and size as a fraction of the monitor (M4 decision: top, 40 %).</summary>
public sealed record DockLayout(DockEdge Edge, double Fraction)
{
    public static DockLayout Default { get; } = new(DockEdge.Top, 0.4);

    /// <summary>The strip must leave enough room for working underneath it.</summary>
    public DockLayout Normalized() => this with { Fraction = Math.Clamp(Fraction, 0.15, 0.7) };
}
