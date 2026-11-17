using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Finds the installation from the path of the running executable – only the one in
/// Program Files\ZoomIN. The elevated steps sign, trust and remove certificates for this layout
/// (ADR-0031): a copy in a user-writable folder must never be one, or its owner could swap the
/// unpacked ZoomIN.exe between check and signing (review 2026-11-17).
/// </summary>
public sealed class ProcessRunningInstall : IRunningInstall
{
    public bool TryGetLayout(out InstallLayout layout)
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ZoomIN");
        return InstallLayout.TryFromExecutable(Environment.ProcessPath, out layout) && layout.IsAt(expected);
    }
}
