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
    private static readonly HttpClient s_http = CreateClient();
    private readonly ILogger _log;

    public GitHubReleaseFeed(ILogger<GitHubReleaseFeed> log)
    {
        _log = log;
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
            using var response = await s_http.GetAsync(feed, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return GitHubReleaseParser.Parse(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _log.ReleaseListSkipped(ex.Message);
            return [];
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // GitHub's API rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoomIN", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
