using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>A window the shortcut acts on.</summary>
/// <param name="Kind">App + window class.</param>
/// <param name="Palette">What it shows now (<see cref="WindowPalette"/>).</param>
/// <param name="Bounds">Visible frame in physical desktop pixels, to find its monitor.</param>
public readonly record struct WindowFilterTarget(WindowKind Kind, byte Palette, PhysicalRect Bounds);
