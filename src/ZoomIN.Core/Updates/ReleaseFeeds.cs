namespace ZoomIN.Core.Updates;

/// <summary>Where releases are published (ADR-0014). The repository is public, no token needed.</summary>
public static class ReleaseFeeds
{
    /// <summary>
    /// Up to 100 newest releases; unauthenticated requests are limited to 60 per hour and IP,
    /// plenty for a check at start-up and once a day.
    /// </summary>
    public static Uri GitHub { get; } = new("https://api.github.com/repos/gartenkoch7-ctrl/ZoomIN/releases?per_page=100");
}
