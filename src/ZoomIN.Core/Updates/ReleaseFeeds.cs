namespace ZoomIN.Core.Updates;

/// <summary>Where releases are published (ADR-0014). The repository is public, no token needed.</summary>
public static class ReleaseFeeds
{
    /// <summary>
    /// Up to 100 newest releases; unauthenticated requests are limited to 60 per hour and IP,
    /// plenty for a check at start-up and once a day.
    /// </summary>
    public static Uri GitHub { get; } = new("https://api.github.com/repos/gartenkoch7-ctrl/ZoomIN/releases?per_page=100");

    /// <summary>
    /// checksums.txt of the release tagged v<paramref name="version"/>; fixed here, never taken from
    /// a caller, so the elevated install step can trust it (ADR-0031).
    /// </summary>
    public static Uri Checksums(AppVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new($"https://github.com{DownloadPath}v{version}/{ReleaseAssets.ChecksumsName}");
    }

    /// <summary>Path prefix of every release asset of this repository.</summary>
    public const string DownloadPath = "/gartenkoch7-ctrl/ZoomIN/releases/download/";
}
