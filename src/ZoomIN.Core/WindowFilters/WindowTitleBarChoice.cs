namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// What the title bar of a filtered window gets. In the dark Windows theme many apps (above all
/// Windows' own) draw a dark title bar over a light page; inverting it with the page makes it glare.
/// </summary>
public enum WindowTitleBarChoice
{
    /// <summary>Measured on its own: a dark title bar stays like a dark window, a light one is filtered with the window.</summary>
    Automatic,

    /// <summary>Always filtered together with the window.</summary>
    LikeWindow,

    /// <summary>Never the window's own filter; it shows what a dark window shows (the monitor filter).</summary>
    Never,
}
