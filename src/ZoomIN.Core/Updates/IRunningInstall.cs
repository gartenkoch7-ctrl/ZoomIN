namespace ZoomIN.Core.Updates;

/// <summary>
/// The installation the running ZoomIN.exe belongs to (ADR-0015), so flows that change the
/// installation can be tested against a folder of their own (M14 step 8).
/// </summary>
public interface IRunningInstall
{
    /// <summary>False for developer builds and the portable ZIP: they have no installation to change.</summary>
    bool TryGetLayout(out InstallLayout layout);
}
