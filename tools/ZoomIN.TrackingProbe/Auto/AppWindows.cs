using System.Diagnostics;
using ZoomIN.TrackingProbe.Interop;

namespace ZoomIN.TrackingProbe.Auto;

/// <summary>Finding, fronting and closing the windows of applications the automatic run started.</summary>
internal static class AppWindows
{
    /// <summary>The first process of that name not in <paramref name="existing"/> that shows a window.</summary>
    public static (Process? Process, nint Window) WaitFor(string name, HashSet<int> existing, TimeSpan timeout)
    {
        var until = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < until)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                if (!existing.Contains(p.Id) && !p.HasExited && p.MainWindowHandle != 0)
                {
                    return (p, p.MainWindowHandle);
                }

                p.Dispose();
            }

            Thread.Sleep(250);
        }

        return (null, 0);
    }

    /// <summary>The foreground process, if it has that name and was started by this run.</summary>
    public static Process? NewProcessInFront(string name, HashSet<int> existing)
    {
        ProbeNative.GetWindowThreadProcessId(ProbeNative.GetForegroundWindow(), out var pid);
        if (pid == 0 || existing.Contains((int)pid))
        {
            return null;
        }

        try
        {
            var process = Process.GetProcessById((int)pid);
            if (string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase))
            {
                return process;
            }

            process.Dispose();
        }
        catch (ArgumentException)
        {
            // Exited in the meantime.
        }

        return null;
    }

    public static bool BringToFront(nint window)
    {
        if (SameProcessInFront(window))
        {
            return true;
        }

        // Windows only lets the foreground thread hand the foreground over; attaching to its input
        // queue for the moment of the call is the documented way around that for a test tool.
        var foregroundThread = ProbeNative.GetWindowThreadProcessId(ProbeNative.GetForegroundWindow(), out _);
        var own = ProbeNative.GetCurrentThreadId();
        ProbeNative.AttachThreadInput(own, foregroundThread, true);
        ProbeNative.ShowWindow(window, 9); // SW_RESTORE
        ProbeNative.SetForegroundWindow(window);
        ProbeNative.AttachThreadInput(own, foregroundThread, false);
        Thread.Sleep(300);
        return SameProcessInFront(window);
    }

    /// <summary>Process name and title of a window, for the report.</summary>
    public static string Describe(nint window)
    {
        var title = new char[128];
        var length = ProbeNative.GetWindowText(window, title, title.Length);
        ProbeNative.GetWindowThreadProcessId(window, out var pid);
        string name;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            name = process.ProcessName;
        }
        catch (ArgumentException)
        {
            name = $"pid {pid}";
        }

        return $"{name} „{new string(title, 0, length)}“";
    }

    /// <summary>Closes a process this run started: politely first, then by force.</summary>
    public static void Close(Process? process)
    {
        if (process is null)
        {
            return;
        }

        using (process)
        {
            if (process.HasExited)
            {
                return;
            }

            process.CloseMainWindow();
            if (!process.WaitForExit(TimeSpan.FromSeconds(8)))
            {
                // Started by this run only (see WaitFor), so ending it loses nothing of the user's.
                process.Kill(entireProcessTree: true);
            }
        }
    }

    /// <summary>Closes every process of that name that was not running before this application's test.</summary>
    public static void CloseNew(string name, HashSet<int> existing)
    {
        foreach (var process in Process.GetProcessesByName(name))
        {
            if (existing.Contains(process.Id))
            {
                process.Dispose();
                continue;
            }

            try
            {
                Close(process);
            }
            catch (InvalidOperationException)
            {
                // Exited on its own while closing the main process.
            }
        }
    }

    // A process may show its work in another top-level window than its main window (Excel).
    private static bool SameProcessInFront(nint window)
    {
        ProbeNative.GetWindowThreadProcessId(window, out var wanted);
        ProbeNative.GetWindowThreadProcessId(ProbeNative.GetForegroundWindow(), out var front);
        return wanted != 0 && wanted == front;
    }
}
