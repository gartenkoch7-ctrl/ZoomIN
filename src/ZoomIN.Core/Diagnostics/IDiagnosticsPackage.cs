namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// The ZIP a user attaches to a bug report (ADR-0019): kept log and operations files plus a short
/// info file. Nothing else – no settings, no paths beyond file names, no window contents.
/// </summary>
public interface IDiagnosticsPackage
{
    /// <summary>The logs of the secure instance can be added (an elevated copy is available, M10).</summary>
    bool CanIncludeSecureLogs { get; }

    /// <summary>Writes a new package into the diagnostics folder.</summary>
    /// <param name="options">The recording level, as shown in the info file.</param>
    /// <param name="includeSecureLogs">Ask for administrator rights and add the secure instance's logs.</param>
    DiagnosticsPackageResult Create(TelemetryOptions options, bool includeSecureLogs = false);
}
