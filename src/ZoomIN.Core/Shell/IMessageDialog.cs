namespace ZoomIN.Core.Shell;

/// <summary>
/// Standard Windows message boxes for moments without a WinUI window (uninstall). Screen readers,
/// high contrast and text scaling work with them out of the box.
/// </summary>
public interface IMessageDialog
{
    DialogAnswer AskYesNoCancel(string title, string text);

    void Inform(string title, string text, bool error = false);
}
