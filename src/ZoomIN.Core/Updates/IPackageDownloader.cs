namespace ZoomIN.Core.Updates;

/// <summary>Downloads a release package and checks it against the release's SHA-256 list.</summary>
public interface IPackageDownloader
{
    /// <param name="release">The release to fetch.</param>
    /// <param name="directory">Where the package is stored; created if missing.</param>
    /// <param name="progress">Receives 0..1 while downloading, if the size is known.</param>
    /// <param name="cancellationToken">Cancels the download; a partial file is removed.</param>
    Task<PackageDownload> DownloadAsync(
        ReleaseInfo release, string directory, IProgress<double>? progress, CancellationToken cancellationToken);
}
