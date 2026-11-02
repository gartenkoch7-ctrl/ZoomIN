using System.Text.Json;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Turns the GitHub "list releases" response into installable releases. Drafts, tags that are no
/// version and releases without package or checksums are skipped: the setup could not install them.
/// The channel follows the version (pre-release label), not GitHub's "prerelease" flag.
/// </summary>
public static class GitHubReleaseParser
{
    public static IReadOnlyList<ReleaseInfo> Parse(JsonElement releases)
    {
        var result = new List<ReleaseInfo>();
        if (releases.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var release in releases.EnumerateArray())
        {
            if (TryRead(release, out var info))
            {
                result.Add(info);
            }
        }

        return result;
    }

    private static bool TryRead(JsonElement release, out ReleaseInfo info)
    {
        info = null!;
        if (release.ValueKind != JsonValueKind.Object ||
            (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) ||
            !AppVersion.TryParse(String(release, "tag_name"), out var version) ||
            // GitHub sends null for unpublished releases; TryGetDateTimeOffset throws on non-strings.
            !release.TryGetProperty("published_at", out var publishedText) ||
            publishedText.ValueKind != JsonValueKind.String ||
            !publishedText.TryGetDateTimeOffset(out var published) ||
            !release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var package = AssetUrl(assets, ReleaseAssets.PackageName(version));
        var checksums = AssetUrl(assets, ReleaseAssets.ChecksumsName);
        if (package is null || checksums is null)
        {
            return false;
        }

        info = new ReleaseInfo(version, published, package, checksums);
        return true;
    }

    private static Uri? AssetUrl(JsonElement assets, string name)
    {
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind == JsonValueKind.Object && String(asset, "name") == name &&
                Uri.TryCreate(String(asset, "browser_download_url"), UriKind.Absolute, out var url) &&
                url.Scheme == Uri.UriSchemeHttps)
            {
                return url;
            }
        }

        return null;
    }

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
