using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;
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

        var result = new UpdateApplier(new AuthenticodeVerifier(), log).Apply(layout, command);
        if (result == UpdateResult.Installed)
        {
            try
            {
                // Windows "Apps" shows the version from the registry; the shortcut is refreshed too.
                InstallRegistration.Register(layout, command.Version);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                log.Error("[Update] Refreshing the Apps entry failed", ex);
            }

            SecureDesktopInstall.Apply(layout, command.SecureDesktop, log);
        }

        return (int)result;
    }
}
