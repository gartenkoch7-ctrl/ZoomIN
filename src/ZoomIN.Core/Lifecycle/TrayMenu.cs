namespace ZoomIN.Core.Lifecycle;

/// <summary>Tooltip and menu of the tray icon, already in the UI language.</summary>
public sealed record TrayMenu(string Tooltip, IReadOnlyList<TrayMenuItem> Items);
