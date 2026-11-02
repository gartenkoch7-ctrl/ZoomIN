using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class ReleaseCatalogTests
{
    private static readonly ReleaseInfo[] s_releases =
    [
        Release("1.0.0-rc1"),
        Release("1.1.0-dev.3"),
        Release("1.0.0"),
        Release("1.0.1"),
    ];

    [Fact]
    public void Stable_OffersFinishedReleasesNewestFirst()
    {
        var offered = ReleaseCatalog.Offered(s_releases, ReleaseChannel.Stable);

        Assert.Equal(["1.0.1", "1.0.0"], offered.Select(r => r.Version.ToString()));
    }

    [Fact]
    public void Experimental_AlsoOffersCandidatesAndDevelopBuilds()
    {
        var offered = ReleaseCatalog.Offered(s_releases, ReleaseChannel.Experimental);

        Assert.Equal(["1.1.0-dev.3", "1.0.1", "1.0.0", "1.0.0-rc1"], offered.Select(r => r.Version.ToString()));
    }

    [Theory]
    [InlineData(ReleaseChannel.Stable, "1.0.0", "1.0.1")]
    [InlineData(ReleaseChannel.Experimental, "1.0.0", "1.1.0-dev.3")]
    [InlineData(ReleaseChannel.Stable, "1.0.1", null)]
    [InlineData(ReleaseChannel.Stable, "1.1.0-dev.3", null)]
    public void Update_IsNewestOfferedAboveCurrent(ReleaseChannel channel, string current, string? expected)
    {
        var version = V(current);

        var update = ReleaseCatalog.Update(s_releases, channel, version);

        Assert.Equal(expected, update?.Version.ToString());
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", UpdateKind.Upgrade)]
    [InlineData("1.0.1", "1.0.0", UpdateKind.Downgrade)]
    [InlineData("1.1.0-dev.3", "1.0.1", UpdateKind.Downgrade)]
    [InlineData("1.0.0", "1.0.0", UpdateKind.Reinstall)]
    public void Classify_ComparesVersions(string current, string target, UpdateKind expected)
    {
        var from = V(current);
        var to = V(target);

        Assert.Equal(expected, ReleaseCatalog.Classify(from, to));
    }

    [Fact]
    public void PackageName_ContainsVersion()
    {
        Assert.Equal("ZoomIN-1.1.0-dev.3-win-x64-uiaccess.zip", ReleaseAssets.PackageName(new AppVersion(1, 1, 0, "dev.3")));
    }

    private static ReleaseInfo Release(string tag)
    {
        var version = V(tag);
        var url = new Uri($"https://example.invalid/{tag}");
        return new ReleaseInfo(version, DateTimeOffset.UnixEpoch, url, url);
    }

    private static AppVersion V(string text)
    {
        Assert.True(AppVersion.TryParse(text, out var version), text);
        return version;
    }
}
