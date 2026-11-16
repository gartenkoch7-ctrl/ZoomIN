using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Islands;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>Islands in the settings (M19 B1): per profile, shortcuts shared, never on the secure desktop.</summary>
public sealed class IslandSettingsTests
{
    private static readonly IslandEntry s_clock = new()
    {
        MonitorId = IslandMonitors.First, SourceX = 1800, SourceY = 1040, SourceWidth = 120, SourceHeight = 40,
        TargetX = 1560, TargetY = 960, Level = 3, Still = true,
    };

    [Fact]
    public void FactorySettings_NoIslands_ShownAndShortcutsSet()
    {
        var settings = new AppSettings();

        Assert.Empty(settings.Islands.Items);
        Assert.True(settings.Islands.Visible);
        Assert.Equal(HotkeyGesture.ToggleIslands, settings.IslandsHotkey);
        Assert.Equal(HotkeyGesture.EditIslands, settings.IslandEditHotkey);
        Assert.Equal(0x49, HotkeyGesture.ToggleIslands.VirtualKey);
        Assert.True(HotkeyGesture.EditIslands.Modifiers.HasFlag(HotkeyModifiers.Shift));
        Assert.False(HotkeyRules.BlocksAltGrCharacter(HotkeyGesture.ToggleIslands));
    }

    [Fact]
    public void Mapping_RoundTrips()
    {
        var settings = new AppSettings { Islands = new IslandOptions { Visible = false, Items = [s_clock] } };

        var model = IslandMapping.Create(settings);
        var back = IslandMapping.Apply(new AppSettings(), model);

        Assert.False(back.Islands.Visible);
        Assert.Equal([s_clock], back.Islands.Items);
        Assert.Equal(new PhysicalRect(1560, 960, 360, 120), model.Scene.Set[0].Target);
    }

    [Fact]
    public void Mapping_OlderFileWithoutIslands_GivesDefaults()
    {
        var settings = new AppSettings { Islands = null! };

        var model = IslandMapping.Create(settings);

        Assert.True(model.Scene.Visible);
        Assert.Equal(0, model.Scene.Set.Count);
    }

    [Fact]
    public void Mapping_IgnoresNullEntries_AndMoreThanFour()
    {
        var settings = new AppSettings { Islands = new IslandOptions { Items = [null!, s_clock, s_clock, s_clock, s_clock, s_clock] } };

        Assert.Equal(4, IslandMapping.Set(settings).Count);
    }

    [Fact]
    public void ApplyTo_TakesTheProfilesIslands()
    {
        var model = new IslandModel(IslandSet.Empty, visible: true);

        IslandMapping.ApplyTo(model, new AppSettings { Islands = new IslandOptions { Visible = false, Items = [s_clock] } });

        Assert.Equal(1, model.Scene.Set.Count);
        Assert.False(model.Scene.Visible);
    }

    [Fact]
    public void Islands_BelongToTheProfile()
    {
        var profile = new AppSettings { Islands = new IslandOptions { Items = [s_clock] } };

        var applied = ProfileFields.ApplyDisplay(new AppSettings(), profile);

        Assert.Same(profile.Islands, applied.Islands);
    }

    [Fact]
    public void SecureDesktop_HasNoIslands_AndNoShortcuts()
    {
        var sanitized = SecureSettingsSanitizer.Sanitize(new AppSettings { Islands = new IslandOptions { Items = [s_clock] } });

        Assert.Empty(sanitized.Islands.Items);
        Assert.False(sanitized.Islands.Visible);
        Assert.Equal(default, sanitized.IslandsHotkey);
        Assert.Equal(default, sanitized.IslandEditHotkey);
    }
}
