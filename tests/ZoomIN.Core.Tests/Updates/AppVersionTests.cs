using ZoomIN.Core.Updates;

namespace ZoomIN.Core.Tests.Updates;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("v1.0.0", 1, 0, 0, "")]
    [InlineData("1.2.3", 1, 2, 3, "")]
    [InlineData("V1.0.0-rc1", 1, 0, 0, "rc1")]
    [InlineData("v1.1.0-dev.42", 1, 1, 0, "dev.42")]
    [InlineData("1.0.0+abc123", 1, 0, 0, "")]
    public void ValidTags_AreParsed(string tag, int major, int minor, int patch, string preRelease)
    {
        Assert.True(AppVersion.TryParse(tag, out var version));
        Assert.Equal(new AppVersion(major, minor, patch, preRelease), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.0.x")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-dev..1")]
    [InlineData("latest")]
    public void InvalidTags_AreRejected(string? tag)
    {
        Assert.False(AppVersion.TryParse(tag, out _));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.9", "1.1.0")]
    [InlineData("1.9.9", "2.0.0")]
    [InlineData("1.1.0-dev.9", "1.1.0-dev.10")]
    [InlineData("1.1.0-dev.42", "1.1.0-rc1")]
    [InlineData("1.1.0-rc1", "1.1.0")]
    [InlineData("1.1.0-rc", "1.1.0-rc.1")]
    [InlineData("1.1.0-1", "1.1.0-alpha")]
    [InlineData("1.0.0", "1.1.0-dev.1")]
    public void Precedence_FollowsSemVer(string lower, string higher)
    {
        var low = V(lower);
        var high = V(higher);

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= high && high >= low);
    }

    [Fact]
    public void BuildMetadata_DoesNotAffectOrder()
    {
        var a = V("1.0.0+a");
        var b = V("1.0.0+b");

        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void ToString_RoundTripsWithoutPrefix()
    {
        var version = V("v1.1.0-dev.42");

        Assert.Equal("1.1.0-dev.42", version.ToString());
        Assert.True(version.IsPreRelease);
        Assert.Equal("1.0.0", new AppVersion(1, 0, 0).ToString());
    }

    [Fact]
    public void Null_RanksLowest()
    {
        Assert.Equal(1, new AppVersion(0, 0, 0).CompareTo(null));
    }

    private static AppVersion V(string text)
    {
        Assert.True(AppVersion.TryParse(text, out var version), text);
        return version;
    }
}
