using System.ComponentModel;
using System.Diagnostics;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// schtasks.exe from System32. Chosen over the Task Scheduler COM API: one XML file describes the
/// whole task, and the same command works for a user without admin rights (verified 2026-11-16).
/// </summary>
public sealed class SchtasksProcess : ISchtasks
{
    // schtasks answers in well under a second; the settings page calls it on the UI thread, so a
    // hung Task Scheduler may hold it for this long at most (-1 is returned then).
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    /// <summary>Exit code reported when schtasks did not answer in time and was ended.</summary>
    public const int TimedOut = -1;

    /// <summary>Exit code reported when schtasks could not be started.</summary>
    public const int NotStarted = -2;

    public int Run(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return NotStarted;
            }

            // Read both streams so a full pipe cannot block schtasks.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(s_timeout))
            {
                process.Kill();
                return TimedOut;
            }

            Task.WaitAll(output, error);
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            // Never the native code: it would share values with schtasks exit codes (1 = "missing").
            return NotStarted;
        }
    }
}
