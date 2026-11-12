namespace ZoomIN.Core.Tracking;

/// <summary>One request of <see cref="ViewCenterRequests"/>.</summary>
/// <param name="MonitorId">Monitor whose view should move.</param>
/// <param name="Placement">Point, zoom and time of the request, as the anchor takes it.</param>
public sealed record ViewCenterRequest(string MonitorId, ViewPlacement Placement);
