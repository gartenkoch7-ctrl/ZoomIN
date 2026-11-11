namespace ZoomIN.Core.Tracking;

/// <summary>When a highlight (crosshair, caret marker, focus frame) is shown (E1). Stored by name.</summary>
public enum ShowCondition
{
    /// <summary>All the time (factory setting, behaviour before E1).</summary>
    Always,

    /// <summary>Only after a rest: no mouse movement and no typing for a second.</summary>
    Idle,

    /// <summary>Only while working: mouse moved or typed within the last second.</summary>
    Moving,

    /// <summary>Only while the hold key is held (after a short minimum time).</summary>
    HoldKey,
}
