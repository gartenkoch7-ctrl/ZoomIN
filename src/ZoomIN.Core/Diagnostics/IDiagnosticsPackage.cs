namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// The ZIP a user attaches to a bug report (ADR-0019): kept log and operations files plus a short
/// info file. Nothing else – no settings, no paths beyond file names, no window contents.
/// </summary>
public interface IDiagnosticsPackage
{
    /// <summary>Writes a new package into the diagnostics folder and returns its path.</summary>
    /// <param name="options">The recording level, as shown in the info file.</param>
    string Create(TelemetryOptions options);
}
