using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// The autostart this ZoomIN uses (ADR-0030): an installed copy starts through the Ease of Access
/// list (<see cref="EaseOfAccessAutostart"/>), the portable ZIP and developer builds through the Run
/// key (decision Q10). An installed copy keeps exactly one of both: switching removes the other, so
/// an old Run value never starts a second ZoomIN (which would only open the settings window).
/// </summary>
public sealed class AutostartSelector : IAutostart
{
    private readonly RegistryAutostart _run;
    private readonly EaseOfAccessAutostart? _list;
    private readonly SecureDesktopRegistration _machine;
    private readonly ILogger _log;

    /// <summary>For the DI container: decided by the running exe.</summary>
    public AutostartSelector(ILogger<AutostartSelector> log)
        : this(Environment.ProcessPath, SecureDesktopRegistration.Default, Registry.CurrentUser, RegistryAutostart.RunKey, log)
    {
    }

    /// <param name="executable">The running exe; only an installation in Program Files uses the list.</param>
    /// <param name="machine">HKLM; the list only works with the AT entry there.</param>
    /// <param name="user">HKCU (the Ease of Access list); tests pass a private key.</param>
    /// <param name="runKey">The Run key below HKCU; tests pass a private path.</param>
    internal AutostartSelector(string? executable, SecureDesktopRegistration machine, RegistryKey user, string runKey, ILogger log)
    {
        _run = new RegistryAutostart(runKey, $"\"{RegistryAutostart.StableExecutablePath(executable)}\"");
        _list = InstallLayout.TryFromExecutable(executable, out _) ? new EaseOfAccessAutostart(user) : null;
        _machine = machine;
        _log = log;
    }

    /// <summary>Runs from Program Files (not the ZIP or a developer build).</summary>
    public bool IsInstalled => _list is not null;

    /// <summary>
    /// The list is in charge. Windows ignores list names without an AT entry under HKLM; it is
    /// missing until an elevated step of this version ran (an update is applied by the previous
    /// version), and until then the Run key keeps working. Read at every call: the elevated step
    /// may run while the app is open.
    /// </summary>
    public bool UsesEaseOfAccess => _list is not null && _machine.IsAtRegistered;

    /// <summary>A "ZoomIN" Run value exists, whatever it points at.</summary>
    public bool HasRunValue => _run.Exists;

    /// <summary>"ease_of_access" or "run", for logs.</summary>
    public string Method => UsesEaseOfAccess ? "ease_of_access" : "run";

    /// <summary>
    /// With the list in charge, a Run value left over (migration waiting for the AT entry) starts
    /// ZoomIN too, so it counts as on until the next switch or start removes it.
    /// </summary>
    public bool IsEnabled => UsesEaseOfAccess ? _list!.IsEnabled || _run.Exists : _run.IsEnabled;

    /// <summary>The setup: the copy just installed at <paramref name="layout"/>, as the signed-in user.</summary>
    public static AutostartSelector ForInstall(InstallLayout layout, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new(layout.CurrentExecutable, SecureDesktopRegistration.Default, Registry.CurrentUser, RegistryAutostart.RunKey, log);
    }

    public void SetEnabled(bool enabled)
    {
        // The new way first: a failure in between must not leave the user without autostart.
        var easeOfAccess = UsesEaseOfAccess;
        if (easeOfAccess)
        {
            _list!.SetEnabled(enabled);
            _run.SetEnabled(false);
        }
        else
        {
            _run.SetEnabled(enabled);
            _list?.SetEnabled(false);
        }

        _log.AutostartSwitched(enabled ? "on" : "off", easeOfAccess ? "ease_of_access" : "run");
    }
}
