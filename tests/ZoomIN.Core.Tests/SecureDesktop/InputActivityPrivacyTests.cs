using System.Reflection;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tests.SecureDesktop;

/// <summary>
/// ADR-0016: the keyboard hook also runs while a password is typed on the secure desktop. Its only
/// way out is <see cref="IInputActivityHandler"/>, which must never carry which key was pressed.
/// </summary>
public sealed class InputActivityPrivacyTests
{
    [Fact]
    public void KeyboardEvents_CarryNoKeyInformation()
    {
        var methods = typeof(IInputActivityHandler).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(["CtrlTapped", "MouseMoved", "Typed"], methods.Select(m => m.Name).Order(StringComparer.Ordinal));
        Assert.Empty(methods.Single(m => m.Name == "Typed").GetParameters());
        Assert.Empty(methods.Single(m => m.Name == "CtrlTapped").GetParameters());
        Assert.Equal(["timestamp", "x", "y"], methods.Single(m => m.Name == "MouseMoved").GetParameters().Select(p => p.Name));
    }
}
