namespace ZoomIN.Core.Lifecycle;

/// <summary>Notification-area icon with a context menu (M7). Commands arrive on the tray thread.</summary>
public interface ITrayIcon : IDisposable
{
    event Action<TrayCommand>? Command;

    /// <summary>Shows the icon, or updates tooltip and menu if it is already shown.</summary>
    void Show(TrayMenu menu);

    /// <summary>Short Windows notification next to the icon.</summary>
    void Notify(string title, string message);
}
