using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --apply-update …": the elevated install step without any UI (ADR-0015). It is the
/// installed, signed ZoomIN.exe itself, because Windows only starts UIAccess executables from
/// Program Files; the exit code is the <see cref="UpdateResult"/>.
/// </summary>
internal static class UpdateRunner
{
    public static int Run(UpdateCommand command, ILog log)
    {
        if (!InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout))
        {
            log.Error($"[Update] Not a versioned installation: {Environment.ProcessPath}");
            return (int)UpdateResult.NotInstalled;
        }

        return (int)new UpdateApplier(new AuthenticodeVerifier(), log).Apply(layout, command);
    }
}
