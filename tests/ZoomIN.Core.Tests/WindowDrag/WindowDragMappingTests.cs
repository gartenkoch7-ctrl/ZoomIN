using ZoomIN.Core.Modes;
using ZoomIN.Core.Settings;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.WindowDrag;

/// <summary>The drag options in the settings and profiles (G1: everything belongs to the profile).</summary>
public sealed class WindowDragMappingTests
{
    [Fact]
    public void Options_RoundTripThroughTheSettings()
    {
        var chosen = new WindowDragOptions { Action = WindowDragAction.Lower, LowerLevel = 3, OnMove = false, OnResize = true };
        var settings = new AppSettings { WindowDrag = chosen };
        using var drag = WindowDragMapping.Create(settings, ZoomMapping.Create(settings));

        Assert.Equal(chosen, drag.Options);
        Assert.Equal(chosen, WindowDragMapping.Apply(new AppSettings(), drag).WindowDrag);
    }

    [Fact]
    public void MissingOrBrokenEntry_GivesTheDefaults()
    {
        Assert.Equal(WindowDragOptions.Default, new AppSettings().WindowDrag);
        Assert.Equal(WindowDragOptions.Default, WindowDragMapping.Options(new AppSettings { WindowDrag = null! }));
        Assert.Equal(8, WindowDragMapping.Options(new AppSettings { WindowDrag = new WindowDragOptions { LowerLevel = 99 } }).LowerLevel);
    }

    [Fact]
    public void Profile_CarriesTheDragOptions()
    {
        var profile = new AppSettings { WindowDrag = new WindowDragOptions { Action = WindowDragAction.Unchanged } };

        var applied = ProfileFields.ApplyDisplay(new AppSettings(), profile);

        Assert.Equal(WindowDragAction.Unchanged, applied.WindowDrag.Action);
    }

    [Fact]
    public void TemporaryLevelOfADrag_IsNeverSaved()
    {
        var settings = new AppSettings { MonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY1"] = 4 } };
        var model = ZoomMapping.Create(settings);
        model.Toggle(@"\\.\DISPLAY1", [@"\\.\DISPLAY1"]);
        using var drag = WindowDragMapping.Create(settings, model);

        drag.Start(WindowDragKind.Move, @"\\.\DISPLAY1");
        var saved = ZoomMapping.Apply(settings, model);

        Assert.Equal(4, saved.MonitorZoomLevels[@"\\.\DISPLAY1"]);
        Assert.Equal(4, model.LastLevelFor(ZoomMode.Fullscreen, @"\\.\DISPLAY1"));
    }
}
