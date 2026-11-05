namespace ZoomIN.Core.WindowFilters;

/// <summary>How a top-level window takes part in the filter per window.</summary>
public enum WindowRole
{
    /// <summary>An app window or dialog: measured and filtered on its own.</summary>
    Normal,

    /// <summary>Menu, tooltip or drop-down list: shows what its owner shows, so it never flashes white.</summary>
    Popup,

    /// <summary>Taskbar, Start, desktop and similar: never filtered automatically, but covers windows below.</summary>
    Shell,
}
