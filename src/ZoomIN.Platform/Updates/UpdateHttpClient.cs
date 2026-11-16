using System.Net.Http.Headers;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// The one HTTP client of ZoomIN (ADR-0025): release list and downloads share its connections.
/// No overall timeout, because a 70 MB package on a slow line may take minutes; each call brings
/// its own (the release list 15 s, downloads are cancelled by the caller).
/// </summary>
public sealed class UpdateHttpClient : HttpClient
{
    public UpdateHttpClient()
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan;

        // GitHub's API rejects requests without a User-Agent.
        DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZoomIN", "1.0"));
    }
}
