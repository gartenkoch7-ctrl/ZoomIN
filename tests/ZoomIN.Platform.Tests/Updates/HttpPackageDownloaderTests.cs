using System.Net;
using System.Security.Cryptography;
using System.Text;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>Serves fake release assets from memory; files go to a private temp folder.</summary>
public sealed class HttpPackageDownloaderTests : IDisposable
{
    private static readonly byte[] s_package = Encoding.UTF8.GetBytes("pretend this is a zip");
    private static readonly ReleaseInfo s_release = new(
        new AppVersion(1, 0, 0), DateTimeOffset.UnixEpoch,
        new Uri("https://example.invalid/package.zip"), new Uri("https://example.invalid/checksums.txt"));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZoomIN.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MatchingChecksum_KeepsPackage()
    {
        var progress = new List<double>();
        var result = await Download(Checksums(SHA256.HashData(s_package)), s_package, Token, new Progress(progress));

        Assert.Equal(PackageCheck.Ok, result.Result);
        Assert.Equal(s_package, await File.ReadAllBytesAsync(result.Path!, Token));
        Assert.Equal(1.0, progress[^1]);
    }

    [Fact]
    public async Task WrongChecksum_DeletesPackage()
    {
        var result = await Download(Checksums(new byte[32]), s_package, Token);

        Assert.Equal(PackageCheck.ChecksumMismatch, result.Result);
        Assert.Null(result.Path);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task MissingChecksum_DownloadsNothing()
    {
        var result = await Download("0000  other.zip", s_package, Token);

        Assert.Equal(PackageCheck.ChecksumMissing, result.Result);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ServerError_IsReportedAsDownloadFailed()
    {
        var result = await Download(Checksums(SHA256.HashData(s_package)), package: null, Token);

        Assert.Equal(PackageCheck.DownloadFailed, result.Result);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Cancellation_IsPassedOn()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Download(Checksums(SHA256.HashData(s_package)), s_package, cancel.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Checksums(byte[] hash) =>
        $"{Convert.ToHexStringLower(hash)}  {ReleaseAssets.PackageName(s_release.Version)}\n";

    private async Task<PackageDownload> Download(
        string checksums, byte[]? package, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        using var http = new HttpClient(new FakeServer(checksums, package));
        var downloader = new HttpPackageDownloader(NullLog.Instance, http);
        return await downloader.DownloadAsync(s_release, _directory, progress, cancellationToken);
    }

    private sealed class Progress(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }

    private sealed class FakeServer(string checksums, byte[]? package) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = request.RequestUri!.AbsolutePath.EndsWith("checksums.txt", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(checksums) }
                : package is null
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) };
            return Task.FromResult(response);
        }
    }
}
