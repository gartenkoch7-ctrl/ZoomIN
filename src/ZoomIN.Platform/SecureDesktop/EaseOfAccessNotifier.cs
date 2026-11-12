using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Tells Windows' Ease of Access that the tray app started or exits (ADR-0016). Windows starts a
/// registered AT on the lock screen, Ctrl+Alt+Del and UAC only if it believes the AT is already
/// running; an AT started outside Ease of Access (our autostart, the Start menu) is unknown to it
/// until it sets HKCU "AccessibilityTemp\&lt;AT name&gt;" and injects Win+U (Microsoft Learn,
/// "Ease of Access – Assistive Technology Registration"). Observed 2026-11-03: without this,
/// Windows never started ZoomIN on Win+L.
/// <para>
/// Since ADR-0030 an installed ZoomIN usually starts through the Ease of Access list; Windows then
/// knows it already, and the start signal is skipped. It stays for every other start (Start menu,
/// the Run key of an older setup, after an update), and the exit signal stays for every start.
/// </para>
/// </summary>
public sealed class EaseOfAccessNotifier
{
    internal const string TempPath = @"Software\Microsoft\Windows NT\CurrentVersion\AccessibilityTemp";
    internal const int Starting = 3;
    internal const int Exiting = 2;

    private readonly RegistryKey _user;
    private readonly Func<bool> _secureDesktopsOn;
    private readonly Func<bool> _atRegistered;
    private readonly Action _sendWinU;
    private readonly ILogger _log;
    private readonly bool _startedByEaseOfAccess;
    private bool _announced;

    /// <param name="user">HKCU; tests pass a private key instead.</param>
    /// <param name="secureDesktopsOn">The start signal only matters with the secure desktops on (implies the AT entry).</param>
    /// <param name="atRegistered">Only a registered AT may signal, else Win+U just opens Settings.</param>
    /// <param name="sendWinU">Injects Win+U; tests count the calls instead.</param>
    /// <param name="log">Where the signals are logged.</param>
    /// <param name="startedByEaseOfAccess">Windows started this instance itself (<see cref="LaunchOrigin.ByEaseOfAccess"/>): no start signal.</param>
    public EaseOfAccessNotifier(
        RegistryKey user, Func<bool> secureDesktopsOn, Func<bool> atRegistered, Action sendWinU, ILogger log, bool startedByEaseOfAccess)
    {
        _user = user;
        _secureDesktopsOn = secureDesktopsOn;
        _atRegistered = atRegistered;
        _sendWinU = sendWinU;
        _log = log;
        _startedByEaseOfAccess = startedByEaseOfAccess;
    }

    public static EaseOfAccessNotifier Create(ILogger log, LaunchOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var machine = SecureDesktopRegistration.Default;
        return new(Registry.CurrentUser, () => machine.IsRegistered, () => machine.IsAtRegistered, SendWinU, log, origin.ByEaseOfAccess);
    }

    public void NotifyStarted()
    {
        if (_startedByEaseOfAccess)
        {
            _log.NotifierNotNeeded();
            return;
        }

        if (_secureDesktopsOn())
        {
            _announced = Notify(Starting);
        }
    }

    /// <summary>
    /// Whenever Windows knows ZoomIN runs: after our own start signal, or when Windows started it.
    /// Per the same Microsoft page an AT started by Ease of Access that ends itself must say so,
    /// else Windows keeps starting it on the lock screen and UAC.
    /// </summary>
    public void NotifyExiting()
    {
        if (_announced || (_startedByEaseOfAccess && _atRegistered()))
        {
            Notify(Exiting);
        }
    }

    private bool Notify(int signal)
    {
        try
        {
            using var key = _user.CreateSubKey(TempPath);
            key.SetValue(SecureDesktopRegistration.AtName, signal, RegistryValueKind.DWord);
            _sendWinU();
            _log.EaseOfAccessNotified(signal == Starting ? "starting" : "exiting");
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Without the signal only the lock screen stays unmagnified; never worth failing the app.
            _log.EaseOfAccessNotNotified(ex.GetType().Name);
            return false;
        }
    }

    private static void SendWinU()
    {
        Span<INPUT> inputs = stackalloc INPUT[4];
        SetKey(ref inputs[0], VIRTUAL_KEY.VK_LWIN, up: false);
        SetKey(ref inputs[1], VIRTUAL_KEY.VK_U, up: false);
        SetKey(ref inputs[2], VIRTUAL_KEY.VK_U, up: true);
        SetKey(ref inputs[3], VIRTUAL_KEY.VK_LWIN, up: true);
        PInvoke.SendInput(inputs, System.Runtime.CompilerServices.Unsafe.SizeOf<INPUT>());
    }

    private static void SetKey(ref INPUT input, VIRTUAL_KEY key, bool up)
    {
        input.type = INPUT_TYPE.INPUT_KEYBOARD;
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0;
    }
}
