using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// The logger factory every service gets. Deliberately not Microsoft's LoggerFactory: building
/// that one costs ~20 ms at start-up (measured 2026-11-05), and all it would do is forward to the
/// telemetry host's own provider.
/// </summary>
internal sealed class HostLoggerFactory(SwitchableLoggerProvider provider) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => provider.CreateLogger(categoryName);

    // Everything goes through the telemetry host; other providers are not supported.
    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException("Logging is configured by TelemetryHost only.");

    public void Dispose()
    {
        // The host disposes the provider.
    }
}
