using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Tells Windows' Ease of Access that the tray app started or exits (ADR-0016). Windows starts a
/// registered AT on the lock screen, Ctrl+Alt+Del and UAC only if it believes the AT is already
/// running; an AT started outside Ease of Access (our autostart, the Start menu) is unknown to it
/// until it sets HKCU "AccessibilityTemp\&lt;AT name&gt;" and injects Win+U (Microsoft Learn,
/// "Ease of Access – Assistive Technology Registration"). Observed 2026-11-03: without this,
/// Windows never started ZoomIN on Win+L.
/// </summary>
public sealed class EaseOfAccessNotifier
{
    internal const string TempPath = @"Software\Microsoft\Windows NT\CurrentVersion\AccessibilityTemp";
    internal const int Starting = 3;
    internal const int Exiting = 2;

    private readonly RegistryKey _user;
    private readonly Func<bool> _isRegistered;
    private readonly Action _sendWinU;
    private readonly ILogger _log;

    /// <param name="user">HKCU; tests pass a private key instead.</param>
    /// <param name="isRegistered">Only a registered AT may signal, else Win+U just opens Settings.</param>
    /// <param name="sendWinU">Injects Win+U; tests count the calls instead.</param>
    public EaseOfAccessNotifier(RegistryKey user, Func<bool> isRegistered, Action sendWinU, ILogger log)
    {
        _user = user;
        _isRegistered = isRegistered;
        _sendWinU = sendWinU;
        _log = log;
    }

    public static EaseOfAccessNotifier Create(ILogger log) =>
        new(Registry.CurrentUser, () => SecureDesktopRegistration.Default.IsRegistered, SendWinU, log);

    public void NotifyStarted() => Notify(Starting);

    public void NotifyExiting() => Notify(Exiting);

    private void Notify(int signal)
    {
        if (!_isRegistered())
        {
            return;
        }

        try
        {
            using var key = _user.CreateSubKey(TempPath);
            key.SetValue(SecureDesktopRegistration.AtName, signal, RegistryValueKind.DWord);
            _sendWinU();
            _log.EaseOfAccessNotified(signal == Starting ? "starting" : "exiting");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Without the signal only the lock screen stays unmagnified; never worth failing the app.
            _log.EaseOfAccessNotNotified(ex.GetType().Name);
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
