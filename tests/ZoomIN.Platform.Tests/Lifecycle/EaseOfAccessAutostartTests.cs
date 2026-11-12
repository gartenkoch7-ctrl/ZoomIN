using Microsoft.Win32;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Works on a private key under HKCU, never the real Ease of Access list.</summary>
public sealed class EaseOfAccessAutostartTests : IDisposable
{
    private readonly TestRegistry _registry = new("EaseOfAccess");

    private EaseOfAccessAutostart Autostart => new(_registry.Root);

    [Fact]
    public void Empty_IsOff_AndOnAddsZoomIN()
    {
        Assert.False(Autostart.IsEnabled);

        Autostart.SetEnabled(true);

        Assert.True(Autostart.IsEnabled);
        Assert.Equal("ZoomIN", List());
    }

    [Fact]
    public void OtherAssistiveTechnologies_StayUntouched()
    {
        SetList("nvda_nvda_v1");

        Autostart.SetEnabled(true);
        Assert.Equal("nvda_nvda_v1,ZoomIN", List());

        Autostart.SetEnabled(false);
        Assert.Equal("nvda_nvda_v1", List());
    }

    [Theory]
    [InlineData("zoomin")]
    [InlineData(" ZoomIN , nvda_nvda_v1")]
    [InlineData("nvda_nvda_v1,ZoomIN,ZoomIN")]
    public void Listed_InAnySpelling_IsOn_AndNotAddedTwice(string list)
    {
        SetList(list);

        Assert.True(Autostart.IsEnabled);
        Autostart.SetEnabled(true);
        Assert.Equal(list, List());
    }

    [Fact]
    public void Off_RemovesEveryCopy_AndKeepsOthers()
    {
        SetList("ZoomIN, nvda_nvda_v1 ,zoomin");

        Autostart.SetEnabled(false);

        Assert.False(Autostart.IsEnabled);
        Assert.Equal("nvda_nvda_v1", List());
    }

    [Fact]
    public void Off_WithoutList_CreatesNothing()
    {
        Autostart.SetEnabled(false);

        using var key = _registry.Root.OpenSubKey(AtStartList.KeyPath);
        Assert.Null(key);
    }

    public void Dispose() => _registry.Dispose();

    private void SetList(string list)
    {
        using var key = _registry.Root.CreateSubKey(AtStartList.KeyPath);
        key.SetValue("Configuration", list);
    }

    private string? List()
    {
        using var key = _registry.Root.OpenSubKey(AtStartList.KeyPath);
        return key?.GetValue("Configuration") as string;
    }
}
