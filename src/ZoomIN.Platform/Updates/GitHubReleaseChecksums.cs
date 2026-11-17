using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Reads checksums.txt of a release from its fixed GitHub address (<see cref="ReleaseFeeds.Checksums"/>)
/// in the elevated install step (ADR-0031). Synchronous: the step has no UI and waits anyway.
/// </summary>
public sealed class GitHubReleaseChecksums(HttpClient http, ILogger log) : IReleaseChecksums
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    public byte[]? Published(AppVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var address = ReleaseFeeds.Checksums(version);

        // Defence in depth: whatever a version string contains, only this repository's releases count.
        if (address.Host != "github.com" || !address.AbsolutePath.StartsWith(ReleaseFeeds.DownloadPath, StringComparison.Ordinal) ||
            address.Query.Length > 0 || address.Fragment.Length > 0)
        {
            log.PackageRefused(ReleaseAssets.PackageName(version), "unexpected checksum address");
            return null;
        }

        try
        {
            using var cancel = new CancellationTokenSource(s_timeout);
            var content = http.GetStringAsync(address, cancel.Token).GetAwaiter().GetResult();
            if (ChecksumList.TryFind(content, ReleaseAssets.PackageName(version), out var sha256))
            {
                return sha256;
            }

            log.PackageRefused(ReleaseAssets.PackageName(version), "not listed in the published checksums");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            log.PackageRefused(ReleaseAssets.PackageName(version), "published checksum unavailable: " + ex.GetType().Name);
            return null;
        }
    }
}
