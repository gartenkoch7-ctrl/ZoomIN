using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Shell;

/// <summary>
/// Notices waiting for <see cref="Win32TrayIcon"/> to show them on its thread (M17 A1). Without
/// the icon (shell not ready, Explorer restarting) they wait; only the newest few, as each notice
/// replaces the one before anyway (one balloon per icon). Windows only says "a balloon was
/// clicked", also later from the notification centre: the click belongs to the notice shown last.
/// </summary>
internal sealed class TrayNotices
{
    private const int MaxWaiting = 4;

    private readonly Queue<TrayNotice> _waiting = new();
    private readonly Lock _gate = new();

    /// <summary>What a click on a notice does: the command of the notice shown last.</summary>
    public TrayCommand? Click { get; private set; }

    /// <summary>Any thread.</summary>
    public void Add(TrayNotice notice)
    {
        lock (_gate)
        {
            _waiting.Enqueue(notice);
            while (_waiting.Count > MaxWaiting)
            {
                _waiting.Dequeue();
            }
        }
    }

    /// <summary>Tray thread: the oldest waiting notice, left in place until Windows showed it.</summary>
    public bool TryPeek(out TrayNotice notice)
    {
        lock (_gate)
        {
            return _waiting.TryPeek(out notice);
        }
    }

    /// <summary>Tray thread: Windows showed <paramref name="notice"/>.</summary>
    public void Shown(TrayNotice notice)
    {
        Remove(notice);
        Click = notice.Clicked;
    }

    /// <summary>Tray thread: Windows refused it although the icon is there; it would block the newer ones.</summary>
    public void Drop(TrayNotice notice) => Remove(notice);

    private void Remove(TrayNotice notice)
    {
        lock (_gate)
        {
            // Add may have trimmed it meanwhile; then it is no longer the oldest.
            if (_waiting.TryPeek(out var first) && first == notice)
            {
                _waiting.Dequeue();
            }
        }
    }
}
