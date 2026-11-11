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

    /// <summary>"ZoomIN.Mcp.exe" (or "ZoomIN.exe --mcp"): the KI-Debug MCP server for an agent (ADR-0027).</summary>
    Mcp,
}
