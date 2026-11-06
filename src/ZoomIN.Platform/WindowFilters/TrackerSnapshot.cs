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
