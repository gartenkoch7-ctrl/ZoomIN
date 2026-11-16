using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// The autostart this ZoomIN uses (ADR-0030): an installed copy starts through a scheduled task at
/// sign-in (<see cref="TaskSchedulerAutostart"/>, Q9), the portable ZIP and developer builds through
/// the Run key (Q10). An installed copy keeps only the task: switching removes a Run value and the
/// Ease of Access list entry of earlier versions. That list started the tray app in a job that
/// Windows ends at the next lock screen (spike A1, 2026-11-16).
/// </summary>
public sealed class AutostartSelector : IAutostart
{
    private readonly RegistryAutostart _run;
    private readonly TaskSchedulerAutostart? _task;
    private readonly EaseOfAccessAutostart? _list;
    private readonly ILogger _log;

    /// <summary>For the DI container: decided by the running exe.</summary>
    public AutostartSelector(ILogger<AutostartSelector> log)
        : this(Environment.ProcessPath, Registry.CurrentUser, RegistryAutostart.RunKey, new SchtasksProcess(), null, log)
    {
    }

    /// <param name="executable">The running exe; only an installation in Program Files uses the task.</param>
    /// <param name="user">HKCU (the Ease of Access list); tests pass a private key.</param>
    /// <param name="runKey">The Run key below HKCU; tests pass a private path.</param>
    /// <param name="schtasks">schtasks.exe; tests pass a fake.</param>
    /// <param name="userSid">The user the task belongs to; null reads the signed-in user (only when installed).</param>
    internal AutostartSelector(string? executable, RegistryKey user, string runKey, ISchtasks schtasks, string? userSid, ILogger log)
    {
        var stable = RegistryAutostart.StableExecutablePath(executable);
        _run = new RegistryAutostart(runKey, $"\"{stable}\"");
        if (InstallLayout.TryFromExecutable(executable, out _))
        {
            _task = new TaskSchedulerAutostart(stable!, userSid ?? CurrentUserSid(), schtasks, log);
            _list = new EaseOfAccessAutostart(user);
        }

        _log = log;
    }

    private static string CurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? throw new InvalidOperationException("The current user has no SID.");
    }

    /// <summary>Runs from Program Files (not the ZIP or a developer build).</summary>
    public bool IsInstalled => _task is not null;

    /// <summary>A "ZoomIN" Run value exists, whatever it points at.</summary>
    public bool HasRunValue => _run.Exists;

    /// <summary>"ZoomIN" is in the user's Ease of Access start list (earlier versions wrote it).</summary>
    public bool HasListEntry => _list?.IsEnabled == true;

    /// <summary>The autostart task exists.</summary>
    public bool HasTask => _task?.IsEnabled == true;

    /// <summary>"task" or "run", for logs.</summary>
    public string Method => IsInstalled ? "task" : "run";

    /// <summary>Leftovers of earlier versions start ZoomIN too, so they count as on until removed.</summary>
    public bool IsEnabled => IsInstalled ? HasTask || HasRunValue || HasListEntry : _run.IsEnabled;

    /// <summary>The setup: the copy just installed at <paramref name="layout"/>, as the signed-in user.</summary>
    public static AutostartSelector ForInstall(InstallLayout layout, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new(layout.CurrentExecutable, Registry.CurrentUser, RegistryAutostart.RunKey, new SchtasksProcess(), null, log);
    }

    public void SetEnabled(bool enabled) => TrySetEnabled(enabled);

    /// <returns>True if the autostart now is as asked (always for the Run key, which throws instead).</returns>
    public bool TrySetEnabled(bool enabled)
    {
        if (_task is null)
        {
            _run.SetEnabled(enabled);
            _log.AutostartSwitched(enabled ? "on" : "off", Method);
            return true;
        }

        // The task first, and the old ways only once it exists: a failure must not leave the user
        // without autostart. Where tasks cannot be created (policy, service off), the Run key starts
        // ZoomIN instead (Explorer uses ShellExecute, so UIAccess works); the migration retries.
        // The list entry goes either way: it starts the tray app in a job that ends at every lock.
        var done = _task.TrySetEnabled(enabled);
        // The Run value only if the task surely is missing: a task that exists unconfirmed plus a Run
        // value would start ZoomIN twice (the second start opens the settings window). Unknown keeps
        // the Run value as it is; the migration sorts it out at the next start.
        if (!enabled || done)
        {
            _run.SetEnabled(false);
        }
        else if (_task.IsSurelyMissing)
        {
            _run.SetEnabled(true);
        }

        _list!.SetEnabled(false);

        _log.AutostartSwitched(enabled ? "on" : "off", Method);
        return done;
    }
}
