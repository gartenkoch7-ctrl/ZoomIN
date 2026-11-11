namespace ZoomIN.Core.WindowDrag;

/// <summary>
/// Zoom while moving windows (G1), stored per profile. Properties use <c>set</c> for the JSON
/// source generator (see the remark on AppSettings); treat instances as immutable.
/// </summary>
public sealed record WindowDragOptions
{
    /// <summary>Level for <see cref="WindowDragAction.Lower"/> (decision: 2×).</summary>
    public const double DefaultLowerLevel = 2.0;

    /// <summary>Range and step of <see cref="LowerLevel"/>, as on the settings page (1× is "lift").</summary>
    public const double MinLowerLevel = 1.5;

    public const double MaxLowerLevel = 8.0;

    public const double LowerLevelStep = 0.5;

    public static WindowDragOptions Default { get; } = new();

    public WindowDragAction Action { get; set; } = WindowDragAction.Lift;

    /// <summary>Ceiling for <see cref="WindowDragAction.Lower"/>; monitors zoomed in less keep their level.</summary>
    public double LowerLevel { get; set; } = DefaultLowerLevel;

    /// <summary>Act while a window is moved (decision: on).</summary>
    public bool OnMove { get; set; } = true;

    /// <summary>Act while a window is resized (decision: on).</summary>
    public bool OnResize { get; set; } = true;

    /// <summary>
    /// Undefined action values (e.g. a number set by hand) become Lift; the level is put on the
    /// page's steps, so the slider never has to change a loaded value (it would be saved unasked).
    /// </summary>
    public WindowDragOptions Normalized() => this with
    {
        Action = Enum.IsDefined(Action) ? Action : WindowDragAction.Lift,
        LowerLevel = double.IsFinite(LowerLevel)
            ? Math.Clamp(Math.Round(LowerLevel / LowerLevelStep, MidpointRounding.AwayFromZero) * LowerLevelStep, MinLowerLevel, MaxLowerLevel)
            : DefaultLowerLevel,
    };
}
