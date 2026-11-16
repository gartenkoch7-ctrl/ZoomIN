using Microsoft.Extensions.Logging;

namespace ZoomIN.Core.Diagnostics;

/// <summary>What the telemetry host records right now, derived from <see cref="TelemetryOptions"/>.</summary>
/// <param name="MinLogLevel">Lowest level written; never above <see cref="LogLevel.Error"/>.</param>
public readonly record struct TelemetryFilter(LogLevel MinLogLevel, bool Traces, bool Metrics)
{
    public bool Allows(LogLevel level) => level != LogLevel.None && level >= MinLogLevel;
}
