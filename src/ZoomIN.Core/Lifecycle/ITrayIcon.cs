namespace ZoomIN.Core.Lifecycle;

/// <summary>Notification-area icon with a context menu (M7). Commands arrive on the tray thread.</summary>
public interface ITrayIcon : IDisposable
{
    event Action<TrayCommand>? Command;

    /// <summary>
    /// The icon got into the notification area: right away, or once Explorer's taskbar exists (an
    /// autostart before it, an Explorer restart). Raised on the tray thread.
    /// </summary>
    event Action? Shown;

    /// <summary>The icon is in the notification area now.</summary>
    bool IsShown { get; }

    /// <summary>Shows the icon, or updates tooltip and menu if it is already shown.</summary>
    void Show(TrayMenu menu);

    /// <summary>Short Windows notification next to the icon.</summary>
    /// <param name="clicked">Raised as <see cref="Command"/> when the user clicks this notice; null = nothing.</param>
    void Notify(string title, string message, TrayCommand? clicked = null);
}
