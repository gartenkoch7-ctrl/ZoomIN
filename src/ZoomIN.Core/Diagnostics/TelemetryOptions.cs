using Microsoft.Extensions.Logging;

namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// Diagnostics settings (ADR-0019). A raised setting – "Verbose", "Custom" or more than one day –
/// only lasts <see cref="RaiseDuration"/> and then falls back to "Normal" and one day, so detailed
/// recording is never left on by accident.
/// </summary>
/// <param name="Logs">Custom only: write warnings/info/debug; errors are written regardless.</param>
/// <param name="LogLevel">Custom only: lowest log level.</param>
/// <param name="RetentionDays">Days of files kept, 1–<see cref="MaxRetentionDays"/>.</param>
/// <param name="RaisedUntil">End of a raised setting; null while nothing is raised.</param>
/// <param name="OtlpEndpoint">OTLP collector address; empty = nothing leaves the PC.</param>
public sealed record TelemetryOptions(
    TelemetryLevel Level,
    bool Logs,
    bool Traces,
    bool Metrics,
    LogLevel LogLevel,
    int RetentionDays,
    DateTimeOffset? RaisedUntil,
    string OtlpEndpoint)
{
    public const int MaxRetentionDays = 7;

    public static readonly TimeSpan RaiseDuration = TimeSpan.FromDays(7);

    public static TelemetryOptions Default { get; } =
        new(TelemetryLevel.Normal, true, true, false, LogLevel.Warning, 1, null, string.Empty);

    /// <summary>More than the default is recorded or kept.</summary>
    public bool IsRaised => Level is TelemetryLevel.Verbose or TelemetryLevel.Custom || RetentionDays > 1;

    /// <summary>What is recorded at this level; errors always are.</summary>
    public TelemetryFilter Filter => Level switch
    {
        TelemetryLevel.ErrorsOnly => new(LogLevel.Error, Traces: false, Metrics: false),
        TelemetryLevel.Verbose => new(LogLevel.Debug, Traces: true, Metrics: true),
        TelemetryLevel.Custom => new(Logs ? Lowest(LogLevel, LogLevel.Error) : LogLevel.Error, Traces, Metrics),
        _ => new(LogLevel.Warning, Traces: true, Metrics: false),
    };

    /// <summary>
    /// The options in force at <paramref name="now"/>: a raised setting whose time is up (or that
    /// has no end, e.g. edited by hand) falls back to "Normal" and one day.
    /// </summary>
    public TelemetryOptions Effective(DateTimeOffset now)
    {
        var options = Normalized();
        if (!options.IsRaised || (options.RaisedUntil is { } until && now < until))
        {
            return options;
        }

        return options with
        {
            Level = options.Level is TelemetryLevel.ErrorsOnly ? TelemetryLevel.ErrorsOnly : TelemetryLevel.Normal,
            RetentionDays = 1,
            RaisedUntil = null,
        };
    }

    /// <summary>
    /// The user chose <paramref name="next"/>: a raised choice gets a fresh seven-day window, a
    /// normal one none.
    /// </summary>
    public static TelemetryOptions Chosen(TelemetryOptions next, DateTimeOffset now)
    {
        var options = next.Normalized();
        return options with { RaisedUntil = options.IsRaised ? now + RaiseDuration : null };
    }

    /// <summary>Values from a file may be out of range (hand edits, older versions).</summary>
    public TelemetryOptions Normalized() => this with
    {
        Level = Enum.IsDefined(Level) ? Level : TelemetryLevel.Normal,
        LogLevel = LogLevel is >= LogLevel.Trace and <= LogLevel.Error ? LogLevel : LogLevel.Warning,
        RetentionDays = Math.Clamp(RetentionDays, 1, MaxRetentionDays),
        OtlpEndpoint = OtlpEndpoint?.Trim() ?? string.Empty,
    };

    private static LogLevel Lowest(LogLevel a, LogLevel b) => a < b ? a : b;
}
