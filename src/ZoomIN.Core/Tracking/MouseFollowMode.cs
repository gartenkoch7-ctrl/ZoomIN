namespace ZoomIN.Core.Tracking;

/// <summary>How the magnified view follows the mouse (M17 A1). Docked always shows the area around the mouse.</summary>
public enum MouseFollowMode
{
    /// <summary>The pointer stays in the middle of the view; the view moves with every mouse step (ADR-0018).</summary>
    Center,

    /// <summary>The view stands still until the pointer reaches the margin, then pushes just far enough (factory default).</summary>
    Margin,

    /// <summary>The pointer is shown at its own desktop position; the view moves proportionally (ADR-0003).</summary>
    Fixpoint,
}
