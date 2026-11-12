using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Shell;

/// <summary>The tray icon's context menu at the mouse (M7); split out of <see cref="Win32TrayIcon"/>.</summary>
internal sealed unsafe class TrayPopupMenu(HWND owner)
{
    /// <summary>Shows the menu and waits; returns the chosen command, or null if the user chose nothing.</summary>
    public TrayCommand? Show(TrayMenu items)
    {
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            foreach (var item in items.Items)
            {
                var flags = item.Text is null ? MENU_ITEM_FLAGS.MF_SEPARATOR
                    : MENU_ITEM_FLAGS.MF_STRING | (item.Checked ? MENU_ITEM_FLAGS.MF_CHECKED : 0);
                fixed (char* text = item.Text)
                {
                    PInvoke.AppendMenu(menu, flags, (nuint)item.Command + 1, text);
                }
            }

            // Required so the menu closes when the user clicks elsewhere.
            PInvoke.SetForegroundWindow(owner);
            PInvoke.GetCursorPos(out var at);
            var chosen = PInvoke.TrackPopupMenu(menu, TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON,
                at.X, at.Y, 0, owner, null);
            PInvoke.PostMessage(owner, PInvoke.WM_NULL, default, default);
            return chosen.Value > 0 ? (TrayCommand)(chosen.Value - 1) : null;
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }
}
