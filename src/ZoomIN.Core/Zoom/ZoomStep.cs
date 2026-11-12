namespace ZoomIN.Core.Zoom;

/// <summary>
/// Outcome of <see cref="ZoomTargetRules.PlanStep"/>: the new targets, the new level, the monitors
/// it applies to and those among them that switched on or off.
/// </summary>
internal sealed record ZoomStep(
    Dictionary<string, double> Targets, double Level, IReadOnlyCollection<string> Stepped, IReadOnlyList<string> Switched);
