using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Tracking;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>What the hold key hook makes of each key event (E1): left and right told apart, typing detected.</summary>
public sealed class HoldKeyCodesTests
{
    [Theory]
    [InlineData(MarkerHoldKey.RightShift, 0xA1)]
    [InlineData(MarkerHoldKey.LeftShift, 0xA0)]
    [InlineData(MarkerHoldKey.RightCtrl, 0xA3)]
    [InlineData(MarkerHoldKey.LeftCtrl, 0xA2)]
    [InlineData(MarkerHoldKey.RightAlt, 0xA5)]
    [InlineData(MarkerHoldKey.LeftAlt, 0xA4)]
    public void EveryHoldKey_HasItsSideSpecificCode(MarkerHoldKey key, int virtualKey)
    {
        var code = (VIRTUAL_KEY)virtualKey;
        Assert.Equal(code, HoldKeyCodes.For(key));
        Assert.Equal(HoldKeyEvent.Down, HoldKeyCodes.Classify(code, 0x36, down: true, up: false, key));
        Assert.Equal(HoldKeyEvent.Up, HoldKeyCodes.Classify(code, 0x36, down: false, up: true, key));
    }

    [Fact]
    public void LeftShift_IsNotTheRightShiftHoldKey()
    {
        Assert.Equal(HoldKeyEvent.None, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_LSHIFT, 0x2A, true, false, MarkerHoldKey.RightShift));
    }

    [Fact]
    public void ALetter_IsTyping()
    {
        Assert.Equal(HoldKeyEvent.Other, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_A, 0x1E, true, false, MarkerHoldKey.RightShift));
        Assert.Equal(HoldKeyEvent.None, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_A, 0x1E, false, true, MarkerHoldKey.RightShift));
    }

    [Fact]
    public void AltGrsLeftCtrl_DoesNotCancelHoldingRightAlt()
    {
        Assert.Equal(HoldKeyEvent.None, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_LCONTROL, 0x1D, true, false, MarkerHoldKey.RightAlt));
    }

    [Fact]
    public void AltGrsLeftCtrl_IsNotAPressOfTheLeftCtrlHoldKey()
    {
        Assert.Equal(HoldKeyEvent.None, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_LCONTROL, 0x21D, true, false, MarkerHoldKey.LeftCtrl));
        Assert.Equal(HoldKeyEvent.Down, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_LCONTROL, 0x1D, true, false, MarkerHoldKey.LeftCtrl));
    }

    [Fact]
    public void UnknownHoldKey_FallsBackToRightShift()
    {
        Assert.Equal(VIRTUAL_KEY.VK_RSHIFT, HoldKeyCodes.For((MarkerHoldKey)99));
    }

    [Theory]
    [InlineData(MarkerHoldKey.RightShift)]
    [InlineData(MarkerHoldKey.LeftCtrl)]
    [InlineData(MarkerHoldKey.RightAlt)]
    public void EscOfTheOverview_IsNeverAHoldKey_OnlyTyping(MarkerHoldKey key)
    {
        // M18 integration: the activity hook swallows Esc while the overview runs (M17 B2). The hold
        // key hook may or may not see it (hook order); either way it is no hold key, at most typing,
        // which ends a running hold like any other key (accepted in the M18 integration).
        Assert.NotEqual(VIRTUAL_KEY.VK_ESCAPE, HoldKeyCodes.For(key));
        Assert.Equal(HoldKeyEvent.Other, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_ESCAPE, 0x01, true, false, key));
        Assert.Equal(HoldKeyEvent.None, HoldKeyCodes.Classify(VIRTUAL_KEY.VK_ESCAPE, 0x01, false, true, key));
    }
}
