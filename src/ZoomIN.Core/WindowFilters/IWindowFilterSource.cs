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

    /// <summary>
    /// The window the colour filter shortcut acts on (ADR-0033): the active one, or the one under
    /// <paramref name="mouse"/> when the active one is the Windows interface; false like <see cref="TryGetForeground"/>.
    /// </summary>
    bool TryGetActive(Viewport.PhysicalPoint? mouse, out WindowFilterTarget window);

    /// <summary>App + class of every window open now (for the detail page), without duplicates.</summary>
    IReadOnlyList<WindowKind> OpenKinds();
}
