namespace ZoomIN.Core.Diagnostics;

/// <summary>What is recorded (diagnostics page, ADR-0019). Errors are recorded at every level.</summary>
public enum TelemetryLevel
{
    /// <summary>Errors only.</summary>
    ErrorsOnly,

    /// <summary>Logs from warning on plus operations (traces). Default.</summary>
    Normal,

    /// <summary>All logs down to debug, operations and metrics.</summary>
    Verbose,

    /// <summary>Own choice of logs, operations, metrics and log level.</summary>
    Custom,
}
