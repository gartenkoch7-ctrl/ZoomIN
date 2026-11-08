using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Reads the release list from the GitHub API (ADR-0014). Sends nothing but the request itself;
/// failures yield an empty list, because a missing update list must never disturb the magnifier.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    // A release list that takes longer is skipped; the next check tries again.
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(15);

    private readonly ILogger _log;
    private readonly HttpClient _http;

    public GitHubReleaseFeed(ILogger<GitHubReleaseFeed> log, HttpClient http)
    {
        _log = log;
        _http = http;
    }

    public async Task<IReadOnlyList<ReleaseInfo>> ReleasesAsync(Uri feed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);
        if (feed.Scheme != Uri.UriSchemeHttps)
        {
            return [];
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(s_timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, feed);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            return GitHubReleaseParser.Parse(json.RootElement);
        }
        // ObjectDisposedException: ZoomIN exits while a check runs (the client belongs to the DI container).
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or ObjectDisposedException)
        {
            _log.ReleaseListSkipped(ex.Message);
            return [];
        }
    }
}
