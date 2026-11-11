namespace ZoomIN.Core.Filters;

/// <summary>
/// One colour of an own scheme that is shown as another (M18, C3). Colours are "#RRGGBB" like the
/// pointer colours. Properties use <c>set</c> for the JSON source generator (see the remark on
/// AppSettings); treat instances as immutable.
/// </summary>
public sealed record ColorReplacement
{
    public const int MaxTolerance = 100;

    /// <summary>Tolerance of a new replacement: catches the colour and its anti-aliased edges.</summary>
    public const int DefaultTolerance = 20;

    public string From { get; set; } = "#FF0000";

    public string To { get; set; } = "#FFD600";

    /// <summary>0–100: how far a colour may be from <see cref="From"/> and still be replaced, with a soft edge.</summary>
    public int Tolerance { get; set; } = DefaultTolerance;
}
