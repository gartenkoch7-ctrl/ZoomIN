using System.Text.Json;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Platform.Tests.Updates;

public sealed class GitHubReleaseParserTests
{
    [Fact]
    public void CompleteRelease_IsParsed()
    {
        var releases = Parse($"[{Release("v1.0.0", Package("1.0.0"), Checksums)}]");

        var release = Assert.Single(releases);
        Assert.Equal("1.0.0", release.Version.ToString());
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero), release.PublishedAt);
        Assert.EndsWith("ZoomIN-1.0.0-win-x64-uiaccess.zip", release.PackageUrl.AbsoluteUri, StringComparison.Ordinal);
        Assert.EndsWith("checksums.txt", release.ChecksumsUrl.AbsoluteUri, StringComparison.Ordinal);
        Assert.Null(release.SetupUrl);
    }

    [Fact]
    public void SetupAsset_IsPickedUp()
    {
        var setup = """{"name":"ZoomIN-Setup.exe","browser_download_url":"https://example.invalid/ZoomIN-Setup.exe"}""";

        var release = Assert.Single(Parse($"[{Release("v1.0.0", Package("1.0.0"), Checksums, setup)}]"));

        Assert.EndsWith("ZoomIN-Setup.exe", release.SetupUrl?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopBuild_KeepsPreReleaseLabel()
    {
        var release = Assert.Single(Parse($"[{Release("v1.1.0-dev.42", Package("1.1.0-dev.42"), Checksums)}]"));

        Assert.True(release.Version.IsPreRelease);
    }

    [Theory]
    [InlineData("""{"tag_name":"v1.0.0","draft":true,"published_at":"2026-11-02T10:00:00Z","assets":[]}""")]
    [InlineData("""{"tag_name":"nightly","published_at":"2026-11-02T10:00:00Z","assets":[]}""")]
    [InlineData("""{"tag_name":"v1.0.0","published_at":null,"assets":[]}""")]
    [InlineData("""{"tag_name":"v1.0.0","published_at":"2026-11-02T10:00:00Z"}""")]
    [InlineData("42")]
    public void UnusableEntries_AreSkipped(string entry)
    {
        Assert.Empty(Parse($"[{entry}]"));
    }

    [Fact]
    public void ReleaseWithoutChecksums_IsSkipped()
    {
        Assert.Empty(Parse($"[{Release("v1.0.0", Package("1.0.0"))}]"));
    }

    [Fact]
    public void PackageOfOtherVersion_IsSkipped()
    {
        // A mislabelled asset must never be installed as this version.
        Assert.Empty(Parse($"[{Release("v1.0.1", Package("1.0.0"), Checksums)}]"));
    }

    [Fact]
    public void PlainHttpAsset_IsSkipped()
    {
        var http = """{"name":"checksums.txt","browser_download_url":"http://example.invalid/checksums.txt"}""";

        Assert.Empty(Parse($"[{Release("v1.0.0", Package("1.0.0"), http)}]"));
    }

    [Fact]
    public void NonArrayResponse_YieldsNothing()
    {
        Assert.Empty(Parse("""{"message":"API rate limit exceeded"}"""));
    }

    private const string Checksums =
        """{"name":"checksums.txt","browser_download_url":"https://example.invalid/checksums.txt"}""";

    private static string Package(string version) =>
        $$"""{"name":"ZoomIN-{{version}}-win-x64-uiaccess.zip","browser_download_url":"https://example.invalid/ZoomIN-{{version}}-win-x64-uiaccess.zip"}""";

    private static string Release(string tag, params string[] assets) =>
        $$"""{"tag_name":"{{tag}}","draft":false,"published_at":"2026-11-02T10:00:00Z","assets":[{{string.Join(',', assets)}}]}""";

    private static IReadOnlyList<Core.Updates.ReleaseInfo> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return GitHubReleaseParser.Parse(document.RootElement);
    }
}
