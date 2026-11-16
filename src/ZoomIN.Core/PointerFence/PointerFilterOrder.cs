namespace ZoomIN.Core.PointerFence;

/// <summary>
/// Order of the move filters (M19 plans B1, A6): island edit mode before axis lock before mouse
/// buffer. Forbidden areas (frozen monitors) are not a filter: the fence applies them after all
/// filters, so no filter can let the pointer onto a frozen monitor.
/// </summary>
public static class PointerFilterOrder
{
    public const int IslandEdit = 100;

    public const int AxisLock = 200;

    public const int MouseBuffer = 300;
}
