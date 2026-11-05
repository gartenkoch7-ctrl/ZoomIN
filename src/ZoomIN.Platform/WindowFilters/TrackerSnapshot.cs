using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>The windows of the last update with their palette indexes; immutable, read from any thread.</summary>
internal sealed record TrackerSnapshot(TrackedWindow[] Windows, byte[] Palette)
{
    public static TrackerSnapshot Empty { get; } = new([], []);

    public bool TryFind(nint handle, out WindowFilterTarget target)
    {
        for (var i = 0; i < Windows.Length; i++)
        {
            if (Windows[i].Handle == handle)
            {
                target = new WindowFilterTarget(Windows[i].Kind, Palette[i], Windows[i].Bounds);
                return Windows[i].Role != WindowRole.Shell;
            }
        }

        target = default;
        return false;
    }

    /// <summary>App windows and dialogs, each app + class once, in front-to-back order.</summary>
    public IReadOnlyList<WindowKind> Kinds() =>
        Windows.Where(w => w.Role == WindowRole.Normal).Select(w => w.Kind).Distinct().ToList();
}
