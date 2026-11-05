namespace ZoomIN.Core.Diagnostics;

/// <summary>What a ZoomIN process is running as; recorded as <see cref="Telemetry.RoleAttribute"/>.</summary>
public enum TelemetryRole
{
    App,
    Watchdog,
    Update,
    Elevated,
    Uninstall,
    Secure,
    Setup,
}
