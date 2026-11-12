namespace ZoomIN.Core.Overview;

/// <summary>Tap or hold (B2 decision): a release within 300 ms of the press is a tap.</summary>
public static class OverviewTiming
{
    public static TimeSpan TapLimit { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>True if the shortcut was down for less than <see cref="TapLimit"/>.</summary>
    public static bool IsTap(TimeSpan held) => held < TapLimit;
}
