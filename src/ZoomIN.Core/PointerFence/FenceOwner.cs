namespace ZoomIN.Core.PointerFence;

/// <summary>Who asks the fence to keep the pointer out of an area; each owner's areas are replaced as a whole.</summary>
public enum FenceOwner
{
    /// <summary>Frozen monitors (G2): always forbidden while frozen.</summary>
    FrozenMonitors,

    /// <summary>Island edit mode (B1).</summary>
    IslandEdit,

    /// <summary>Mouse buffer and axis lock (A6).</summary>
    PointerConstraint,
}
