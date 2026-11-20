using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>The windows of the last update with their palette indexes; immutable, read from any thread.</summary>
internal sealed record TrackerSnapshot(TrackedWindow[] Windows, byte[] Palette)
{
    public static TrackerSnapshot Empty { get; } = new([], []);

    /// <summary>
    /// The window the shortcut acts on. A popup in front (menu, flyout) stands for its owner, because
    /// popups always show what their owner shows. False for the Windows interface and for windows
    /// whose choice cannot be remembered (<see cref="WindowKind.CanHaveRule"/>).
    /// </summary>
    public bool TryFind(nint handle, out WindowFilterTarget target)
    {
        target = default;
        var index = IndexOf(handle);
        if (index >= 0 && Windows[index].Role == WindowRole.Popup)
        {
            index = IndexOf(Windows[index].Owner);
        }

        if (index < 0 || Windows[index].Role is WindowRole.Shell or WindowRole.Popup || !Windows[index].Kind.CanHaveRule)
        {
            return false;
        }

        target = new WindowFilterTarget(Windows[index].Kind, Palette[index], Windows[index].Bounds);
        return true;
    }

    /// <summary>
    /// The window the colour filter shortcut acts on (ADR-0033): the active one; only when that is the
    /// Windows interface (desktop, taskbar) or no listed window, the one under <paramref name="mouse"/>.
    /// An active window that cannot get a rule gives nothing, never an unrelated app under the mouse.
    /// </summary>
    public bool TryFindActive(nint foreground, PhysicalPoint? mouse, out WindowFilterTarget target)
    {
        var index = IndexOf(foreground);
        if (index >= 0 && Windows[index].Role != WindowRole.Shell)
        {
            return TryFind(foreground, out target);
        }

        target = default;
        return mouse is { } point && TryFindAt(point, out target);
    }

    /// <summary>Like <see cref="TryFind"/> for the front-most window holding <paramref name="point"/>.</summary>
    public bool TryFindAt(PhysicalPoint point, out WindowFilterTarget target)
    {
        for (var i = 0; i < Windows.Length; i++)
        {
            if (RectMath.Contains(Windows[i].Bounds, point))
            {
                return TryFind(Windows[i].Handle, out target);
            }
        }

        target = default;
        return false;
    }

    /// <summary>App windows and dialogs that can get a rule, each app + class once, in front-to-back order.</summary>
    public IReadOnlyList<WindowKind> Kinds() =>
        Windows.Where(w => w.Role == WindowRole.Normal && w.Kind.CanHaveRule).Select(w => w.Kind).Distinct().ToList();

    private int IndexOf(nint handle)
    {
        for (var i = 0; handle != 0 && i < Windows.Length; i++)
        {
            if (Windows[i].Handle == handle)
            {
                return i;
            }
        }

        return -1;
    }
}
