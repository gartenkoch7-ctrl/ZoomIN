using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Setup.Install;

/// <summary>
/// The elevated part of the setup (ADR-0015): ends a running ZoomIN, installs the verified package
/// into Program Files\ZoomIN and registers it in Windows "Apps" and the Start menu.
/// </summary>
internal sealed class ElevatedInstall(
    InstallLayout layout, ISignatureVerifier verifier, IInstallRegistration registration, ISecureDesktopInstall secureDesktop, ILogger log)
{
    public UpdateResult Run(UpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // A running ZoomIN is a UIAccess process: only an elevated process may end it. Ending the
        // watchdog too keeps it from restarting the old version.
        foreach (var process in Process.GetProcessesByName("ZoomIN"))
        {
            using (process)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    log.ProcessNotEnded(process.Id, ex.Message);
                }
            }
        }

        var result = new UpdateApplier(verifier, log).Apply(layout, command);
        if (result is UpdateResult.Installed or UpdateResult.AlreadyActive)
        {
            try
            {
                registration.Register(layout, command.Version);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                log.RegistrationFailed(ex);
                return UpdateResult.Failed;
            }

            secureDesktop.Apply(layout, command.SecureDesktop);
        }

        return result;
    }
}
