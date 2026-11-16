using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class TelemetryEndpointTests
{
    [Theory]
    [InlineData("http://localhost:4317")]
    [InlineData("http://LOCALHOST:4318/")]
    [InlineData("http://127.0.0.1:4317")]
    [InlineData("http://127.5.6.7:4317")]
    [InlineData("http://[::1]:4317")]
    public void ThisPc_IsLocal(string address)
    {
        Assert.True(TelemetryEndpoint.IsValid(address));
        Assert.True(TelemetryEndpoint.IsLocal(address));
    }

    [Theory]
    [InlineData("http://192.168.1.20:4317")]
    [InlineData("https://otel.example.com")]
    [InlineData("http://collector:4317")]
    public void OtherHosts_AreValid_ButNotLocal(string address)
    {
        Assert.True(TelemetryEndpoint.IsValid(address));
        Assert.False(TelemetryEndpoint.IsLocal(address));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_IsValid_AndNotLocal(string? address)
    {
        Assert.True(TelemetryEndpoint.IsValid(address));
        Assert.False(TelemetryEndpoint.IsLocal(address));
    }

    [Theory]
    [InlineData("localhost:4317")]
    [InlineData("ftp://localhost")]
    [InlineData("not an address")]
    public void NoHttpAddress_IsInvalid(string address)
    {
        Assert.False(TelemetryEndpoint.IsValid(address));
    }
}
