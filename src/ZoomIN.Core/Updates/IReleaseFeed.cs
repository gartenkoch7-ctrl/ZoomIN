namespace ZoomIN.Core.Updates;

/// <summary>Lists the published releases (GitHub Releases, ADR-0014).</summary>
public interface IReleaseFeed
{
    /// <returns>All installable releases; empty if the feed cannot be reached.</returns>
    Task<IReadOnlyList<ReleaseInfo>> ReleasesAsync(Uri feed, CancellationToken cancellationToken);
}
