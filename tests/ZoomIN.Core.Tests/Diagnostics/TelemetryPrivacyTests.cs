using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tests.Diagnostics;

public sealed class TelemetryPrivacyTests
{
    [Theory]
    [InlineData("process.name")]
    [InlineData("zoomin.window.class")]
    [InlineData("zoomin.tracking.source")]
    [InlineData("zoomin.monitor")]
    [InlineData("exception.type")]
    [InlineData("File")]
    [InlineData("Folder")]
    public void TechnicalAttributes_AreAllowed(string key)
    {
        Assert.True(TelemetryPrivacy.IsAllowedAttribute(key));
    }

    [Theory]
    [InlineData("window.title")]
    [InlineData("zoomin.window.name")]
    [InlineData("zoomin.focus.element.name")]
    [InlineData("zoomin.caret.text")]
    [InlineData("zoomin.update.path")]
    [InlineData("Path")]
    [InlineData("zoomin.browser.url")]
    [InlineData("zoomin.settings.fullpath")]
    [InlineData("DocumentName")]
    public void UserContent_IsRejected(string key)
    {
        Assert.False(TelemetryPrivacy.IsAllowedAttribute(key));
    }
}
