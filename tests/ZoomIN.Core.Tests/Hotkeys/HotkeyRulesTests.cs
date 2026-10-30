using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Core.Tests.Hotkeys;

public sealed class HotkeyRulesTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    [Theory]
    [InlineData(0x51)] // Q → @
    [InlineData(0x45)] // E → €
    [InlineData(0x37)] // 7 → {
    public void CtrlAlt_WithAltGrKey_IsBlocked(int key)
    {
        Assert.True(HotkeyRules.BlocksAltGrCharacter(new HotkeyGesture(CtrlAlt, key)));
    }

    [Fact]
    public void CtrlAlt_WithZ_IsAllowed()
    {
        Assert.False(HotkeyRules.BlocksAltGrCharacter(HotkeyGesture.ToggleZoom));
    }

    [Fact]
    public void CtrlAltShift_WithAltGrKey_IsAllowed()
    {
        // Shift changes the combination; AltGr+Shift+Q is not @.
        Assert.False(HotkeyRules.BlocksAltGrCharacter(new HotkeyGesture(CtrlAlt | HotkeyModifiers.Windows, 0x51)));
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, false)]
    [InlineData(HotkeyModifiers.Shift, false)]
    [InlineData(HotkeyModifiers.Control, true)]
    [InlineData(HotkeyModifiers.Windows | HotkeyModifiers.Shift, true)]
    public void HasModifier_RequiresCtrlAltOrWin(HotkeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, HotkeyRules.HasModifier(new HotkeyGesture(modifiers, 0x5A)));
    }

    [Fact]
    public void AllDefaults_AreValid()
    {
        HotkeyGesture[] defaults =
        [
            HotkeyGesture.ToggleZoom, HotkeyGesture.ZoomIn, HotkeyGesture.ZoomOut, HotkeyGesture.Fullscreen,
            HotkeyGesture.Lens, HotkeyGesture.Docked, HotkeyGesture.ToggleFilter, HotkeyGesture.NextFilter,
            HotkeyGesture.Locate, HotkeyGesture.ToggleTracking, HotkeyGesture.NextProfile,
        ];

        Assert.All(defaults, g => Assert.True(HotkeyRules.HasModifier(g) && !HotkeyRules.BlocksAltGrCharacter(g), g.ToString()));
        Assert.Equal(defaults.Length, defaults.Distinct().Count());
    }
}
