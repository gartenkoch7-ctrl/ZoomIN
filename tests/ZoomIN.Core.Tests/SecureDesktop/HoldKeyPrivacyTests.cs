using System.Reflection;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.SecureDesktop;

/// <summary>
/// ADR-0016: the hold key hook of the highlights (M18) also sees the keyboard on the secure desktop.
/// Its only way out is <see cref="IHoldKeyHandler"/>: held or released, never which other key was pressed.
/// </summary>
public sealed class HoldKeyPrivacyTests
{
    [Fact]
    public void HoldKeyEvents_CarryNoKeyInformation()
    {
        var methods = typeof(IHoldKeyHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .ToList();

        Assert.Equal(["HoldKeyDown", "HoldKeyUp", "OtherKeyDown"], methods.Select(m => m.Name).Order(StringComparer.Ordinal));
        Assert.All(methods, m => Assert.Empty(m.GetParameters()));
    }

    [Fact]
    public void TheOnlyData_IsTheConfiguredKey_NotAPressedOne()
    {
        var properties = typeof(IHoldKeyHandler).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var key = Assert.Single(properties);
        Assert.Equal("Key", key.Name);
        Assert.False(key.CanWrite);
    }
}
