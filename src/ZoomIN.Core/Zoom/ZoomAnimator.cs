namespace ZoomIN.Core.Zoom;

/// <summary>
/// Glides the displayed zoom towards the target. Interpolates in log space so 1×→2× feels as
/// fast as 8×→16×. Exponential easing never overshoots and is frame-rate independent.
/// Owned by the render thread; <see cref="Advance"/> runs every frame and does not allocate.
/// </summary>
public sealed class ZoomAnimator
{
    // Below this log-space distance (≈0.05 %) the animation snaps to the target.
    private const double SettleThreshold = 0.0005;

    public double Current { get; private set; } = ZoomLimits.Min;

    public double Target { get; private set; } = ZoomLimits.Min;

    public bool IsSettled => Current == Target;

    public void SetTarget(double target) => Target = ZoomLimits.Clamp(target);

    /// <param name="deltaSeconds">Time since the previous frame.</param>
    /// <param name="durationSeconds">Time to cover ~95 % of the distance; 0 jumps immediately.</param>
    public void Advance(double deltaSeconds, double durationSeconds)
    {
        if (Current == Target)
        {
            return;
        }

        if (durationSeconds <= 0)
        {
            Current = Target;
            return;
        }

        // e^-3 ≈ 5 %: after durationSeconds only 5 % of the distance is left.
        var alpha = 1.0 - Math.Exp(-3.0 * deltaSeconds / durationSeconds);
        var logTarget = Math.Log(Target);
        var logCurrent = Math.Log(Current);
        logCurrent += (logTarget - logCurrent) * alpha;

        Current = Math.Abs(logTarget - logCurrent) < SettleThreshold ? Target : Math.Exp(logCurrent);
    }
}
