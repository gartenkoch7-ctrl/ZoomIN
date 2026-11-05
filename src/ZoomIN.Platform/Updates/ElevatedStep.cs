using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Starts "&lt;exe&gt; --apply-update …" with the administrator prompt and waits for it (ADR-0015).
/// Used by the in-app updater (installed ZoomIN.exe) and by the setup (ZoomIN-Setup.exe).
/// </summary>
public static class ElevatedStep
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    public static UpdateResult Run(string executable, UpdateCommand command, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            // Through the shell with "runas": CreateProcess can neither elevate nor start the
            // UIAccess build (ADR-0012).
            using var process = Process.Start(new ProcessStartInfo(executable, TraceParentArgument.Append(command.Format()))
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
            {
                return UpdateResult.Failed;
            }

            process.WaitForExit();
            var result = (UpdateResult)process.ExitCode;
            log.InstallStepResult(command.Version, result);
            return Enum.IsDefined(result) ? result : UpdateResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return UpdateResult.Declined;
        }
        catch (Win32Exception ex)
        {
            log.InstallStepFailed(ex);
            return UpdateResult.Failed;
        }
    }

    /// <summary>SHA-256 of the package as lowercase hex, the form <see cref="UpdateCommand"/> carries.</summary>
    public static string HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
    }
}
