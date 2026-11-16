using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Core.Tests.SecureDesktop;

public sealed class AtConfigurationListTests
{
    [Theory]
    [InlineData(null, "ZoomIN")]
    [InlineData("", "ZoomIN")]
    [InlineData("magnifierpane", "magnifierpane,ZoomIN")]
    [InlineData("magnifierpane,ZoomIN", "magnifierpane,ZoomIN")]
    [InlineData("zoomin", "zoomin")]
    public void Add_AppendsOnce_KeepsOthers(string? list, string expected) =>
        Assert.Equal(expected, AtConfigurationList.Add(list, "ZoomIN"));

    [Theory]
    [InlineData("ZoomIN", "")]
    [InlineData("magnifierpane, ZoomIN ,osk", "magnifierpane,osk")]
    [InlineData("osk", "osk")]
    [InlineData(null, "")]
    public void Remove_DropsOnlyZoomIN(string? list, string expected) =>
        Assert.Equal(expected, AtConfigurationList.Remove(list, "ZoomIN"));

    [Fact]
    public void Contains_IgnoresCaseAndSpaces() =>
        Assert.True(AtConfigurationList.Contains("osk, zoomin", "ZoomIN"));
}
