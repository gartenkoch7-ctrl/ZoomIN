using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>Focus and caret of one poll; an estimated caret is the focused element itself.</summary>
internal readonly record struct FocusReading(PhysicalRect? Focus, PhysicalRect? Caret, bool CaretEstimated);
