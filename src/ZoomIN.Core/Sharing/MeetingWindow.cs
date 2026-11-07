using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>A visible top-level window of the meeting app, as the share source sees it.</summary>
/// <param name="Id">Opaque window id (a window handle on Windows), so the source can follow the frame.</param>
/// <param name="Bounds">Window rectangle in physical desktop pixels.</param>
/// <param name="Topmost">Always on top.</param>
/// <param name="ClickThrough">Lets the mouse through (layered + transparent), like a drawn frame.</param>
public readonly record struct MeetingWindow(long Id, PhysicalRect Bounds, bool Topmost, bool ClickThrough);
