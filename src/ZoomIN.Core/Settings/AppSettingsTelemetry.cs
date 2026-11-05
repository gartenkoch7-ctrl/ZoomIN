using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Settings;

/// <summary>
/// Reads and writes the diagnostics part of <see cref="AppSettings"/> as one
/// <see cref="TelemetryOptions"/>. The settings stay flat properties so older files keep their
/// defaults (see the remark on <see cref="AppSettings"/>).
/// </summary>
public static class AppSettingsTelemetry
{
    public static TelemetryOptions Telemetry(this AppSettings settings) => new(
        settings.TelemetryLevel,
        settings.TelemetryLogs,
        settings.TelemetryTraces,
        settings.TelemetryMetrics,
        settings.TelemetryLogLevel,
        settings.TelemetryRetentionDays,
        settings.TelemetryRaisedUntil,
        settings.TelemetryOtlpEndpoint ?? string.Empty);

    public static AppSettings WithTelemetry(this AppSettings settings, TelemetryOptions options) => settings with
    {
        TelemetryLevel = options.Level,
        TelemetryLogs = options.Logs,
        TelemetryTraces = options.Traces,
        TelemetryMetrics = options.Metrics,
        TelemetryLogLevel = options.LogLevel,
        TelemetryRetentionDays = options.RetentionDays,
        TelemetryRaisedUntil = options.RaisedUntil,
        TelemetryOtlpEndpoint = options.OtlpEndpoint,
    };
}
