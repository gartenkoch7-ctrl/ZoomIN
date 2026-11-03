using System.Net.Http.Headers;
using System.Security.Cryptography;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Streams a release package to disk while hashing it, then compares the SHA-256 with the
/// release's checksum list (ADR-0014). Only a matching file keeps its final name; anything else
/// is deleted, so a half-downloaded or tampered package can never be picked up later.
/// </summary>
public sealed class HttpPackageDownloader : IPackageDownloader
{
    private const int BufferSize = 81920;
    private static readonly HttpClient s_http = CreateClient();
    private readonly HttpClient _http;
    private readonly ILog _log;

    public HttpPackageDownloader(ILog log)
        : this(log, s_http)
    {
    }

    internal HttpPackageDownloader(ILog log, HttpClient http)
    {
        _log = log;
        _http = http;
    }

    public Task<PackageDownload> DownloadAsync(
        ReleaseInfo release, string directory, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        return DownloadAssetAsync(release, ReleaseAssets.PackageName(release.Version), release.PackageUrl, directory, progress, cancellationToken);
    }

    public Task<PackageDownload> DownloadSetupAsync(
        ReleaseInfo release, string directory, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        return release.SetupUrl is { } url
            ? DownloadAssetAsync(release, ReleaseAssets.SetupName, url, directory, progress, cancellationToken)
            : Task.FromResult(new PackageDownload(PackageCheck.DownloadFailed, null));
    }

    private async Task<PackageDownload> DownloadAssetAsync(
        ReleaseInfo release, string name, Uri url, string directory, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var target = Path.Combine(directory, name);
        var partial = target + ".partial";
        try
        {
            var checksums = await _http.GetStringAsync(release.ChecksumsUrl, cancellationToken).ConfigureAwait(false);
            if (!ChecksumList.TryFind(checksums, name, out var expected))
            {
                return new PackageDownload(PackageCheck.ChecksumMissing, null);
            }

            Directory.CreateDirectory(directory);
            var actual = await DownloadAndHashAsync(url, partial, progress, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                File.Delete(partial);
                _log.Info($"Package {name} rejected: checksum mismatch.");
                return new PackageDownload(PackageCheck.ChecksumMismatch, null);
            }

            File.Move(partial, target, overwrite: true);
            return new PackageDownload(PackageCheck.Ok, target);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            TryDelete(partial);
            _log.Info($"Package {name} not downloaded: {ex.Message}");
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new PackageDownload(PackageCheck.DownloadFailed, null);
        }
    }

    private async Task<byte[]> DownloadAndHashAsync(
        Uri url, string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        var buffer = new byte[BufferSize];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            if (total > 0)
            {
                progress?.Report((double)received / total.Value);
            }
        }

        return hash.GetHashAndReset();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next download overwrites it; the ".partial" name keeps it from being installed.
        }
    }

    private static HttpClient CreateClient()
    {
        // No overall timeout: a 70 MB package on a slow line may take minutes; the caller cancels.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoomIN", "1.0"));
        return client;
    }
}
