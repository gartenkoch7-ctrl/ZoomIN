using Microsoft.Extensions.Logging;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --apply-update …": the elevated install step without any UI (ADR-0015). It is the
/// installed, signed ZoomIN.exe itself, because Windows only starts UIAccess executables from
/// Program Files; the exit code is the <see cref="UpdateResult"/>.
/// </summary>
internal sealed class UpdateRunner(
    ISignatureVerifier verifier, IInstallRegistration registration, ISecureDesktopInstall secureDesktop, ILogger<UpdateRunner> log)
{
    public int Run(UpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout))
        {
            log.NotVersioned();
            return (int)UpdateResult.NotInstalled;
        }

        var result = new UpdateApplier(verifier, log).Apply(layout, command);
        if (result == UpdateResult.Installed)
        {
            try
            {
                // Windows "Apps" shows the version from the registry; the shortcut is refreshed too.
                registration.Register(layout, command.Version);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                log.AppsEntryFailed(ex);
            }

            secureDesktop.Apply(layout, command.SecureDesktop);
        }

        return (int)result;
    }
}
