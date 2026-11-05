using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Setup.Install;

/// <summary>
/// The elevated part of the setup (ADR-0015): ends a running ZoomIN, installs the verified package
/// into Program Files\ZoomIN and registers it in Windows "Apps" and the Start menu.
/// </summary>
internal static class ElevatedInstall
{
    public static InstallLayout Layout { get; } =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ZoomIN"));

    public static UpdateResult Run(UpdateCommand command, ILogger log)
    {
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

        var result = new UpdateApplier(new AuthenticodeVerifier(), log).Apply(Layout, command);
        if (result is UpdateResult.Installed or UpdateResult.AlreadyActive)
        {
            try
            {
                InstallRegistration.Register(Layout, command.Version);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                log.RegistrationFailed(ex);
                return UpdateResult.Failed;
            }

            SecureDesktopInstall.Apply(Layout, command.SecureDesktop, log);
        }

        return result;
    }
}
