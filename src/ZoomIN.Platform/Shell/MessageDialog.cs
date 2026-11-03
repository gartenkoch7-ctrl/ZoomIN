using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.Shell;

/// <summary>
/// Standard Windows message boxes for moments without a WinUI window (uninstall). Screen readers,
/// high contrast and text scaling work with them out of the box.
/// </summary>
public static class MessageDialog
{
    public enum Answer
    {
        Yes,
        No,
        Cancel,
    }

    public static Answer AskYesNoCancel(string title, string text)
    {
        var result = PInvoke.MessageBox(
            HWND.Null, text, title, MESSAGEBOX_STYLE.MB_YESNOCANCEL | MESSAGEBOX_STYLE.MB_ICONQUESTION | MESSAGEBOX_STYLE.MB_SETFOREGROUND);
        return result switch
        {
            MESSAGEBOX_RESULT.IDYES => Answer.Yes,
            MESSAGEBOX_RESULT.IDNO => Answer.No,
            _ => Answer.Cancel,
        };
    }

    public static void Inform(string title, string text, bool error = false) =>
        PInvoke.MessageBox(
            HWND.Null,
            text,
            title,
            MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_SETFOREGROUND |
            (error ? MESSAGEBOX_STYLE.MB_ICONERROR : MESSAGEBOX_STYLE.MB_ICONINFORMATION));
}
