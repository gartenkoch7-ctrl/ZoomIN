using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>
/// Share filter state: the source thread publishes detections and window moves, hotkeys mark or
/// hide, render threads read <see cref="Current"/> lock-free (atomic reference swap).
/// </summary>
public sealed class ShareModel
{
    private ShareState _state;

    public ShareModel(ShareOptions options)
    {
        _state = new ShareState(options);
    }

    /// <summary>Raised when the effective area, the filter or the options change. Handlers must be fast.</summary>
    public event Action? Changed;

    /// <summary>Raised only when the user changed <see cref="ShareOptions"/>, i.e. something to save.</summary>
    public event Action? OptionsChanged;

    public ShareState Current => Volatile.Read(ref _state);

    public ShareOptions Options => Current.Options;

    /// <summary>Called by the source about twice a second; null = the meeting app shares nothing.</summary>
    public void PublishDetected(SharedArea? detected) => Update(s => s with
    {
        Detected = detected,

        // The hidden share has ended: the next one shows again.
        Suppressed = detected is not null && s.Suppressed,
    });

    /// <summary>New bounds of the marked window; null when it was closed or minimised, which ends the mark.</summary>
    public void PublishManualWindow(long windowId, PhysicalRect? bounds) => Update(s =>
        s.ManualWindow != windowId ? s
        : bounds is null ? s with { ManualWindow = 0, ManualWindowBounds = null }
        : s with { ManualWindowBounds = bounds });

    /// <summary>Hotkey for a window: hides what is shown, otherwise marks the window under the mouse.</summary>
    public void ToggleWindow(long windowId, PhysicalRect bounds) => Toggle(s => s with
    {
        ManualWindow = windowId,
        ManualWindowBounds = bounds,
    });

    /// <summary>Hotkey for a monitor: hides what is shown, otherwise marks the monitor under the mouse.</summary>
    public void ToggleScreen(PhysicalRect monitorBounds) => Toggle(s => s with { ManualScreen = monitorBounds });

    public void SetOptions(ShareOptions options) => Update(s => s with { Options = options });

    private void Toggle(Func<ShareState, ShareState> mark) => Update(s =>
    {
        if (!s.Options.Enabled)
        {
            return s;
        }

        // Decision 2026-11-03: the hotkey first hides whatever is shown, so a wrong detection can
        // be switched off; only when nothing shows does it mark something new.
        var showing = s.HasManual || s.Area is not null;
        return showing
            ? s with { ManualScreen = null, ManualWindow = 0, ManualWindowBounds = null, Suppressed = s.Detected is not null }
            : mark(s with { Suppressed = s.Detected is not null });
    });

    private void Update(Func<ShareState, ShareState> change)
    {
        ShareState current, next;
        do
        {
            current = Volatile.Read(ref _state);
            next = change(current);
            if (ReferenceEquals(next, current))
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _state, next, current) != current);

        var optionsChanged = next.Options != current.Options;
        if (optionsChanged || next.Area != current.Area || next.Matrix != current.Matrix)
        {
            Changed?.Invoke();
        }

        if (optionsChanged)
        {
            OptionsChanged?.Invoke();
        }
    }
}
