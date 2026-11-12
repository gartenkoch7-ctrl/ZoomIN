using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>Runs this ZoomIN.exe elevated with "--copy-secure-logs &lt;folder&gt;" (<see cref="SecureLogCopy"/>).</summary>
public sealed class ElevatedSecureLogsRequest(ILogger<ElevatedSecureLogsRequest> log) : ISecureLogsRequest
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    // No exit code: the step did not run (or did not end in time).
    private const int NotRun = -1;

    // Waiting starts after the UAC prompt was answered (ShellExecute returns only then).
    private static readonly TimeSpan s_stepLimit = TimeSpan.FromMinutes(2);

    public SecureLogsResult Copy(string target)
    {
        var (result, exitCode) = Run(target);

        // The elevated step keeps no log of its own; its exit code tells refused (2) from failed (1).
        var name = result switch
        {
            SecureLogsResult.Copied => "copied",
            SecureLogsResult.Declined => "declined",
            _ => exitCode == SecureLogCopy.Refused ? "refused" : "failed",
        };
        // A refused or failed copy is a warning: it must reach the package at the default level.
        if (result == SecureLogsResult.Failed)
        {
            log.SecureLogsNotCopied(name, exitCode);
        }
        else
        {
            log.SecureLogsRequested(name, exitCode);
        }

        return result;
    }

    private (SecureLogsResult Result, int ExitCode) Run(string target)
    {
        try
        {
            // The folder name comes from SecureLogCopyRule (no quotes, no trailing backslash), so
            // quoting it keeps it one argument; the elevated step checks it again anyway.
            using var process = Process.Start(new ProcessStartInfo(
                Environment.ProcessPath!, TraceParentArgument.Append($"{SecureLogCopyRule.Switch} \"{target}\""))
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
            {
                return (SecureLogsResult.Failed, NotRun);
            }

            // 20 MB from a local disk take seconds; a step stuck longer must not hold the page forever.
            if (!process.WaitForExit(s_stepLimit))
            {
                return (SecureLogsResult.Failed, NotRun);
            }

            return (process.ExitCode == SecureLogCopy.Copied ? SecureLogsResult.Copied : SecureLogsResult.Failed, process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return (SecureLogsResult.Declined, NotRun);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // The package is still written, without these logs; the page must not fall over.
            log.SecureLogsStartFailed(ex);
            return (SecureLogsResult.Failed, NotRun);
        }
    }
}
