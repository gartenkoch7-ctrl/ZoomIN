using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Modes;

/// <summary>One frame of a zoom mode, all in desktop pixels.</summary>
/// <param name="Target">Where the magnified image is drawn.</param>
/// <param name="Source">Which part of the desktop is magnified into <paramref name="Target"/>.</param>
/// <param name="HasFrame">Draw a border around the target (lens, docked).</param>
public readonly record struct ModeFrame(PhysicalRect Target, PhysicalRect Source, bool HasFrame);
