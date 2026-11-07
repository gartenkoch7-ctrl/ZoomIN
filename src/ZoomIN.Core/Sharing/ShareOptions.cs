using ZoomIN.Core.Filters;

namespace ZoomIN.Core.Sharing;

/// <summary>
/// Share filter settings. The filter shows only to the user: the overlay stays excluded from
/// capture, so viewers always see the original colours (ADR-0018).
/// </summary>
/// <param name="Enabled">Share filter on at all; off ignores detection and hotkeys.</param>
/// <param name="AutoDetect">Detect sharing from the meeting app's windows; off leaves only the hotkeys.</param>
/// <param name="Filter">Filter inside the shared area; replaces the normal filter there.</param>
public sealed record ShareOptions(bool Enabled, bool AutoDetect, ColorFilter Filter)
{
    public static ShareOptions Default { get; } = new(true, true, ColorFilter.YellowOnBlack);
}
