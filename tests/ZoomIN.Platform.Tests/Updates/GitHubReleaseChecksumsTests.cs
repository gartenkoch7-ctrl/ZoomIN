using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>ADR-0031: the elevated step reads checksums.txt only from this repository's release.</summary>
public sealed class GitHubReleaseChecksumsTests
{
    private static readonly AppVersion s_version = new(1, 0, 0, "rc1");
    private static readonly string s_hash = new('a', 64);

    [Fact]
    public void ListedPackage_ReturnsItsHash_FromTheFixedAddress()
    {
        var handler = new Handler(HttpStatusCode.OK, $"{s_hash}  {ReleaseAssets.PackageName(s_version)}\n");

        var published = Checksums(handler).Published(s_version);

        Assert.Equal(Convert.FromHexString(s_hash), published);
        Assert.Equal("https://github.com/gartenkoch7-ctrl/ZoomIN/releases/download/v1.0.0-rc1/checksums.txt", handler.Requested?.AbsoluteUri);
    }

    [Fact]
    public void PackageNotListed_ReturnsNull()
    {
        var handler = new Handler(HttpStatusCode.OK, $"{s_hash}  ZoomIN-Setup.exe\n");

        Assert.Null(Checksums(handler).Published(s_version));
    }

    [Fact]
    public void NoSuchRelease_ReturnsNull()
    {
        Assert.Null(Checksums(new Handler(HttpStatusCode.NotFound, "Not Found")).Published(s_version));
    }

    [Fact]
    public void Offline_ReturnsNull()
    {
        Assert.Null(Checksums(new Handler(new HttpRequestException("offline"))).Published(s_version));
    }

    private static GitHubReleaseChecksums Checksums(Handler handler) =>
        new(new HttpClient(handler), NullLogger.Instance);

    private sealed class Handler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _error;

        public Handler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public Handler(Exception error) => _error = error;

        public Uri? Requested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested = request.RequestUri;
            return _error is not null
                ? Task.FromException<HttpResponseMessage>(_error)
                : Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }
}
