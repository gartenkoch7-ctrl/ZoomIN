using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>Runs the elevated install step through the shell with "runas" (ADR-0015).</summary>
public sealed class ShellElevatedStep(ILogger<ShellElevatedStep> log) : IElevatedStep
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    public UpdateResult Run(string executable, UpdateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The elevated process continues this operation (--traceparent below).
        using var operation = UpdateTelemetry.Start("zoomin.update.elevate", command.Version);
        var result = RunElevated(executable, command);
        return operation.Finish(result, UpdateTelemetry.Succeeded(result));
    }

    public string HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private UpdateResult RunElevated(string executable, UpdateCommand command)
    {
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
}
