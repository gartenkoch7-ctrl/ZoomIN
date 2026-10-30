namespace ZoomIN.Core.Lifecycle;

/// <summary>
/// When the watchdog may restart ZoomIN after a crash (ADR-0007): at most three restarts within
/// five minutes, so a crash loop cannot flood the user with flickering restarts.
/// </summary>
public static class RestartPolicy
{
    public const int MaxRestarts = 3;

    public static TimeSpan Window { get; } = TimeSpan.FromMinutes(5);

    /// <param name="crashTimes">All recorded crashes, including the one just detected.</param>
    /// <param name="now">Current time.</param>
    public static bool ShouldRestart(IEnumerable<DateTimeOffset> crashTimes, DateTimeOffset now) =>
        crashTimes.Count(time => time <= now && now - time <= Window) <= MaxRestarts;
}
