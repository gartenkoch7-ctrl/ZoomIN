using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>Finds the installation from the path of the running executable.</summary>
public sealed class ProcessRunningInstall : IRunningInstall
{
    public bool TryGetLayout(out InstallLayout layout) => InstallLayout.TryFromExecutable(Environment.ProcessPath, out layout);
}
