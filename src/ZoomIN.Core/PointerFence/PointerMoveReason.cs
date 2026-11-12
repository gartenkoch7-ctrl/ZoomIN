namespace ZoomIN.Core.PointerFence;

/// <summary>The shortcut that moves the pointer (ADR-0028: only on an explicit command).</summary>
public enum PointerMoveReason
{
    /// <summary>Ctrl+Alt+H froze the monitor under the pointer: it jumps to the nearest free one.</summary>
    FreezeMonitor,

    /// <summary>Ctrl+Alt+N: next monitor.</summary>
    NextMonitor,
}
