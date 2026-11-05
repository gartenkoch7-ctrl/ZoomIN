namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Watches the top-level windows, measures new ones once and publishes the regions to the
/// <see cref="WindowFilterModel"/> (ADR-0020). Implemented in Platform.
/// </summary>
public interface IWindowFilterSource : IDisposable
{
    void Start(WindowFilterModel model);

    /// <summary>The active window as the filter sees it; false for the Windows interface or no window.</summary>
    /// <param name="window">App + class, palette index and visible bounds.</param>
    bool TryGetForeground(out WindowFilterTarget window);

    /// <summary>App + class of every window open now (for the detail page), without duplicates.</summary>
    IReadOnlyList<WindowKind> OpenKinds();
}
