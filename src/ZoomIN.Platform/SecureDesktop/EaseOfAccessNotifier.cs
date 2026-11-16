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
/// An installed ZoomIN starts through a scheduled task (ADR-0030, Q9), which Windows does not know
/// as an AT, so the start signal is sent then too. It is skipped only when Ease of Access started
/// ZoomIN itself (an old list entry); the exit signal stays for every start.
/// </para>
/// </summary>
public sealed class EaseOfAccessNotifier
{
    internal const string TempPath = @"Software\Microsoft\Windows NT\CurrentVersion\AccessibilityTemp";
    internal const int Starting = 3;
    internal const int Exiting = 2;

    /// <summary>How long the start signal waits for the tray icon before it goes out anyway.</summary>
    internal static readonly TimeSpan ShellWaitLimit = TimeSpan.FromSeconds(60);

    private readonly RegistryKey _user;
    private readonly Func<bool> _secureDesktopsOn;
    private readonly Func<bool> _atRegistered;
    private readonly Func<bool> _sendWinU;
    private readonly ILogger _log;
    private readonly bool _startedByEaseOfAccess;
    private readonly Lock _gate = new();
    private bool _announced;
    private bool _exited;
    private int _waiting;

    /// <param name="user">HKCU; tests pass a private key instead.</param>
    /// <param name="secureDesktopsOn">The start signal only matters with the secure desktops on (implies the AT entry).</param>
    /// <param name="atRegistered">Only a registered AT may signal, else Win+U just opens Settings.</param>
    /// <param name="sendWinU">Injects Win+U and says whether Windows took all four keys; tests count the calls instead.</param>
    /// <param name="log">Where the signals are logged.</param>
    /// <param name="startedByEaseOfAccess">Windows started this instance itself (<see cref="LaunchOrigin.ByEaseOfAccess"/>): no start signal.</param>
    public EaseOfAccessNotifier(
        RegistryKey user, Func<bool> secureDesktopsOn, Func<bool> atRegistered, Func<bool> sendWinU, ILogger log, bool startedByEaseOfAccess)
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

    /// <summary>
    /// The start signal once Explorer runs. Win+U is Explorer's shortcut: sent before the taskbar
    /// exists, Windows drops it and never starts ZoomIN on the lock screen (autostart task, Q9, seen
    /// 2026-11-16). The tray icon getting in is the sign that Explorer is there.
    /// </summary>
    /// <param name="settle">Pause after the taskbar appeared, so Explorer has its shortcuts too.</param>
    public void NotifyStartedWhenShellRuns(ITrayIcon tray, TimeSpan settle, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(tray);

        // Nothing to send (Windows started us, or the secure desktops are off): no waiting either.
        if (tray.IsShown || _startedByEaseOfAccess || !_secureDesktopsOn())
        {
            NotifyStarted();
            return;
        }

        _log.WaitingForShell();
        Volatile.Write(ref _waiting, 1);
        Action? shown = null;
        shown = () =>
        {
            if (Interlocked.Exchange(ref _waiting, 0) == 0)
            {
                return;
            }

            tray.Shown -= shown;
            _ = Task.Delay(settle, clock).ContinueWith(_ => NotifyStarted(), TaskScheduler.Default);
        };
        tray.Shown += shown;

        // The icon may have got in between the check and the subscription.
        if (tray.IsShown)
        {
            shown();
        }

        // No icon at all (another shell, Explorer gone): send anyway rather than never.
        _ = Task.Delay(ShellWaitLimit, clock).ContinueWith(
            _ =>
            {
                if (Interlocked.Exchange(ref _waiting, 0) == 1)
                {
                    tray.Shown -= shown;
                    _log.ShellNotSeen(ShellWaitLimit.TotalSeconds);
                    NotifyStarted();
                }
            },
            TaskScheduler.Default);
    }

    public void NotifyStarted()
    {
        lock (_gate)
        {
            if (!_exited)
            {
                Start();
            }
        }
    }

    private void Start()
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
        // The delayed start signal runs on the thread pool: never after this exit signal.
        lock (_gate)
        {
            _exited = true;
            Volatile.Write(ref _waiting, 0);
            if (_announced || (_startedByEaseOfAccess && _atRegistered()))
            {
                Notify(Exiting);
            }
        }
    }

    private bool Notify(int signal)
    {
        try
        {
            using var key = _user.CreateSubKey(TempPath);
            key.SetValue(SecureDesktopRegistration.AtName, signal, RegistryValueKind.DWord);
            // Blocked while another desktop has the input (e.g. Win+L during the settle pause).
            if (!_sendWinU())
            {
                _log.EaseOfAccessNotNotified("SendInput");
                return false;
            }

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

    private static bool SendWinU()
    {
        Span<INPUT> inputs = stackalloc INPUT[4];
        SetKey(ref inputs[0], VIRTUAL_KEY.VK_LWIN, up: false);
        SetKey(ref inputs[1], VIRTUAL_KEY.VK_U, up: false);
        SetKey(ref inputs[2], VIRTUAL_KEY.VK_U, up: true);
        SetKey(ref inputs[3], VIRTUAL_KEY.VK_LWIN, up: true);
        return PInvoke.SendInput(inputs, System.Runtime.CompilerServices.Unsafe.SizeOf<INPUT>()) == inputs.Length;
    }

    private static void SetKey(ref INPUT input, VIRTUAL_KEY key, bool up)
    {
        input.type = INPUT_TYPE.INPUT_KEYBOARD;
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0;
    }
}
