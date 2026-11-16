using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.PointerFence;

/// <summary>
/// The pointer fence ("Zeigerzaun", ADR-0028): keeps the pointer out of areas the user switched
/// on (frozen monitors) and runs move filters (mouse buffer, axis lock). One rectangle is kept
/// with ClipCursor, any other shape or a filter with a low-level mouse hook. The fence re-applies
/// itself after a desktop switch, session unlock and display change, and releases on dispose,
/// process exit and when nothing is allowed: it never leaves the pointer trapped.
/// </summary>
/// <remarks>
/// Move the pointer out of an area before forbidding it: ClipCursor pulls a pointer outside the
/// rectangle onto its edge, the hook lets a pointer already inside a forbidden area move freely.
/// </remarks>
public interface IPointerFence : IDisposable
{
    FenceMode Mode { get; }

    /// <summary>
    /// Replaces the forbidden rectangles of <paramref name="owner"/> (physical desktop pixels);
    /// an empty list removes them. The allowed area is computed against the current monitors.
    /// </summary>
    void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas);

    /// <summary>Adds a move filter; while any is registered the mouse hook runs.</summary>
    void AddFilter(IPointerMoveFilter filter);

    void RemoveFilter(IPointerMoveFilter filter);

    /// <summary>
    /// Raised after the fence recomputed on its own (desktop switch, unlock, display change),
    /// on a background thread. Owners re-check their areas, e.g. G2 unfreezes a removed monitor.
    /// </summary>
    event EventHandler<FenceTrigger>? Recomputed;
}
