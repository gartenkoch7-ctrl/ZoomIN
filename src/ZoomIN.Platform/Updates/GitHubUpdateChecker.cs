using System.Net.Http.Headers;
using System.Text.Json;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Reads "tag_name" of the latest GitHub release (M7, opt-in). Sends nothing but the request
/// itself; failures are silent because an update hint is never important enough to bother.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly HttpClient s_http = CreateClient();
    private readonly ILog _log;

    public GitHubUpdateChecker(ILog log)
    {
        _log = log;
    }

    public async Task<Version?> NewerVersionAsync(string feedUrl, Version current, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        try
        {
            using var response = await s_http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var tag = json.RootElement.GetProperty("tag_name").GetString();
            return Version.TryParse(tag?.TrimStart('v', 'V'), out var latest) && latest > current ? latest : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException or InvalidOperationException)
        {
            _log.Info($"Update check skipped: {ex.Message}");
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // GitHub's API rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoomIN", "1.0"));
        return client;
    }
}
