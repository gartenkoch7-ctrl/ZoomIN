using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>Decides inside the mouse hook; must not allocate, lock, log or throw.</summary>
internal interface IFenceHookTarget
{
    /// <returns>True to swallow the move; the hook then puts the pointer on <paramref name="allowed"/>.</returns>
    bool OnMove(PhysicalPoint current, PhysicalPoint requested, out PhysicalPoint allowed);
}
