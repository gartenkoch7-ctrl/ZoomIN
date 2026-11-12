namespace ZoomIN.Core.Pointer;

/// <summary>
/// Mouse buffer and axis lock (A6, M19), shared by all profiles. Properties use <c>set</c> for the
/// JSON source generator (see the remark on AppSettings); treat instances as immutable.
/// </summary>
public sealed record PointerConstraintOptions
{
    public static PointerConstraintOptions Default { get; } = new();

    /// <summary>Pressing time at a monitor edge before the pointer may cross (decision: off).</summary>
    public EdgeBufferLevel Buffer { get; set; } = EdgeBufferLevel.Off;

    /// <summary>Holding <see cref="AxisLockKey"/> keeps the pointer on one axis (decision: off).</summary>
    public bool AxisLock { get; set; }

    public AxisLockKey AxisLockKey { get; set; } = AxisLockKey.LeftShift;

    /// <summary>Undefined values (e.g. a number set by hand) fall back to the factory values.</summary>
    public PointerConstraintOptions Normalized() => this with
    {
        Buffer = Enum.IsDefined(Buffer) ? Buffer : EdgeBufferLevel.Off,
        AxisLockKey = Enum.IsDefined(AxisLockKey) ? AxisLockKey : AxisLockKey.LeftShift,
    };

    /// <summary>Stored options as they apply: missing (null in the file) means the factory values.</summary>
    public static PointerConstraintOptions Normalize(PointerConstraintOptions? options) =>
        (options ?? Default).Normalized();

    /// <summary>How long the pointer has to press against an edge; zero when the buffer is off.</summary>
    public static TimeSpan PressTime(EdgeBufferLevel level) => level switch
    {
        EdgeBufferLevel.Light => TimeSpan.FromMilliseconds(200),
        EdgeBufferLevel.Medium => TimeSpan.FromMilliseconds(400),
        EdgeBufferLevel.Strong => TimeSpan.FromMilliseconds(800),
        _ => TimeSpan.Zero,
    };
}
