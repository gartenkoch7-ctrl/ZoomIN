using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Moves the tray app out of the Ease of Access job (ADR-0030, spike A1): Windows starts the ATs of
/// its "start after sign-in" list inside a job that, with TerminateOnDesktopSwitch=1, ends at the
/// next lock screen or UAC prompt. The instance in the job starts ZoomIN again with
/// CREATE_BREAKAWAY_FROM_JOB and exits before it takes the single-instance mutex.
/// </summary>
public sealed unsafe class JobBreakaway(ILogger log)
{
    // CreateProcess refuses an exe with uiAccess="true" unless the caller has UIAccess (ADR-0012).
    // The instance Windows started should have it (a uiAccess exe does not start without), so the
    // breakaway path is expected to work; unverified, the spike logs "breakaway" or "shell".
    private const int ErrorElevationRequired = 740;

    /// <summary>Logs what <see cref="LaunchOriginProbe"/> found; the spike's answer is in these lines.</summary>
    public void Report(LaunchOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        log.LaunchOriginFound(origin.InJob, origin.JobLimits, origin.ParentProcess ?? "unknown", origin.LeftJob, origin.ByEaseOfAccess);
        if (origin.LeftJob && origin.InJob)
        {
            log.StillInJob(origin.JobLimits);
        }
        else if (!origin.LeftJob && origin.ByEaseOfAccess && origin.InJob && !origin.BreakawayAllowed)
        {
            log.CannotLeaveJob(origin.JobLimits);
        }
    }

    /// <summary>Starts <paramref name="executable"/> outside the job with <paramref name="args"/> plus the marker.</summary>
    /// <returns>True if the new instance runs; this one must exit without taking the mutex.</returns>
    public bool TryRelaunch(string executable, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var arguments = TraceParentArgument.Append(CommandLine.Join([.. args, LaunchOrigin.LeftJobSwitch]));
        if (TryCreateOutsideJob(executable, arguments, out var processId, out var error))
        {
            log.LeftJob(processId, "breakaway");
            return true;
        }

        if (error == ErrorElevationRequired)
        {
            // The installed exe asks for UIAccess and only the shell may grant it. Whether the
            // shell's process leaves the job is part of the spike: the new instance logs it.
            try
            {
                using var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = true });
                if (process is not null)
                {
                    log.LeftJob(process.Id, "shell");
                    return true;
                }
            }
            catch (Win32Exception ex)
            {
                error = ex.NativeErrorCode;
            }
        }

        log.LeaveJobFailed(error);
        return false;
    }

    private static bool TryCreateOutsideJob(string executable, string arguments, out int processId, out int error)
    {
        processId = 0;
        error = 0;

        // CreateProcess may write into the command line, so it gets its own writable copy.
        var commandLine = (CommandLine.Join([executable]) + " " + arguments + "\0").ToCharArray();
        var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
        PROCESS_INFORMATION info;
        fixed (char* application = executable)
        fixed (char* line = commandLine)
        {
            if (!PInvoke.CreateProcess(
                application, line, null, null, false, PROCESS_CREATION_FLAGS.CREATE_BREAKAWAY_FROM_JOB, null, null, &startup, &info))
            {
                error = Marshal.GetLastPInvokeError();
                return false;
            }
        }

        processId = (int)info.dwProcessId;
        PInvoke.CloseHandle(info.hThread);
        PInvoke.CloseHandle(info.hProcess);
        return true;
    }
}
