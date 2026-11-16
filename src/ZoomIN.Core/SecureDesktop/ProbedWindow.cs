using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.SecureDesktop;

/// <summary>A top-level window of this desktop: handle, window class and frame (never its title).</summary>
/// <param name="Bounds">Window rectangle in physical pixels.</param>
public readonly record struct ProbedWindow(nint Handle, string ClassName, PhysicalRect Bounds);
