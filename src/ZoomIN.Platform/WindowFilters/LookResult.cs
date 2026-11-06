namespace ZoomIN.Platform.WindowFilters;

/// <summary>What one look saw of a window: its page and, on its own, its title bar.</summary>
/// <param name="Handle">The window looked at.</param>
/// <param name="IsLight">The window below the title bar; null if it could not be seen.</param>
/// <param name="TitleBarIsLight">The title bar strip; null if there is none or it could not be seen.</param>
internal readonly record struct LookResult(nint Handle, bool? IsLight, bool? TitleBarIsLight = null);
