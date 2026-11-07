using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Shell;

namespace ZoomIN.Platform.Shell;

/// <summary>Win32 MessageBox: works before or without any WinUI window, with screen readers and high contrast.</summary>
public sealed class Win32MessageDialog : IMessageDialog
{
    public DialogAnswer AskYesNoCancel(string title, string text)
    {
        var result = PInvoke.MessageBox(
            HWND.Null, text, title, MESSAGEBOX_STYLE.MB_YESNOCANCEL | MESSAGEBOX_STYLE.MB_ICONQUESTION | MESSAGEBOX_STYLE.MB_SETFOREGROUND);
        return result switch
        {
            MESSAGEBOX_RESULT.IDYES => DialogAnswer.Yes,
            MESSAGEBOX_RESULT.IDNO => DialogAnswer.No,
            _ => DialogAnswer.Cancel,
        };
    }

    public void Inform(string title, string text, bool error = false) =>
        PInvoke.MessageBox(
            HWND.Null,
            text,
            title,
            MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_SETFOREGROUND |
            (error ? MESSAGEBOX_STYLE.MB_ICONERROR : MESSAGEBOX_STYLE.MB_ICONINFORMATION));
}
