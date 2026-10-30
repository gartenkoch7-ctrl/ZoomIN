namespace ZoomIN.Core.Lifecycle;

/// <summary>One tray menu entry; <see cref="Text"/> null draws a separator.</summary>
public sealed record TrayMenuItem(TrayCommand Command, string? Text, bool Checked = false);
