using System.Collections;
using System.Reflection;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Settings;

/// <summary>
/// The mappers between persisted settings and runtime models (M14 step 9): settings → model →
/// settings must give back every value, or a restart would lose the user's choices.
/// </summary>
public sealed class SettingsMappingRoundTripTests
{
    // Not the defaults: a mapper that forgets a field gives the default back and fails here.
    private static readonly AppSettings s_settings = new AppSettings() with
    {
        FilterEnabled = true,
        ColorFilter = ColorFilter.YellowOnBlack,
        PerMonitorFilter = false,
        MonitorFilters = new Dictionary<string, MonitorFilter> { [@"\\.\DISPLAY2"] = new(true, ColorFilter.Inverted) },
        Contrast = 1.4,
        Brightness = -0.1,
        Smoothing = Smoothing.Pixel,
        ProtectPhotos = false,
        CursorMaxScale = 6,
        CursorUnzoomedScale = 2,
        CursorOverrideWindows = true,
        CursorGrowWithZoom = false,
        CursorColorMode = PointerColorMode.Tint,
        CursorColor = "#112233",
        CursorRim = false,
        CursorRimColor = "#445566",
        CursorRimWidth = 3,
        LocatorColor = "#778899",
        LocatorSize = 200,
        LocatorSeconds = 1.5,
        LocatorTriggers = LocatorTriggers.CtrlKey,
        CrosshairEnabled = true,
        CrosshairAlways = true,
        CrosshairColor = "#AABBCC",
        CrosshairWidth = 5,
        CrosshairOpacity = 0.5,
        HideCursorWhileTyping = true,
        TrackingEnabled = false,
        TrackFocus = false,
        TrackCaret = false,
        PanMilliseconds = 350,
        CaretMargin = 0.3,
        FocusFrame = false,
        CaretMarker = false,
        MouseFollow = MouseFollowMode.Fixpoint,
        MouseMargin = 0.35,
        LastMode = ZoomMode.Lens,
        PerMonitorZoom = false,
        AnimationMilliseconds = 250,
        LastZoomLevel = 3,
        MonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY2"] = 6 },
        LensMonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY2"] = 7 },
        DockedMonitorZoomLevels = new Dictionary<string, double> { [@"\\.\DISPLAY2"] = 8 },
        LensZoomLevel = 4,
        DockedZoomLevel = 5,
        LensWidth = 0.5,
        LensHeight = 0.25,
        DockEdge = DockEdge.Left,
        DockFraction = 0.3,
    };

    [Fact]
    public void Effects_RoundTrip()
    {
        var back = EffectsMapping.Apply(new AppSettings(), EffectsMapping.Create(s_settings));

        AssertSame(s_settings, back, "FilterEnabled", "ColorFilter", "PerMonitorFilter", "MonitorFilters", "Contrast", "Brightness",
            "Smoothing", "ProtectPhotos", "CursorMaxScale", "CursorUnzoomedScale", "CursorOverrideWindows", "CursorGrowWithZoom",
            "CursorColorMode", "CursorColor", "CursorRim", "CursorRimColor", "CursorRimWidth", "LocatorColor", "LocatorSize",
            "LocatorSeconds", "LocatorTriggers", "CrosshairEnabled", "CrosshairAlways", "CrosshairColor", "CrosshairWidth",
            "CrosshairOpacity", "HideCursorWhileTyping");
    }

    [Fact]
    public void Tracking_RoundTrip()
    {
        var back = TrackingMapping.Apply(new AppSettings(), TrackingMapping.Create(s_settings));

        AssertSame(s_settings, back, "TrackingEnabled", "TrackFocus", "TrackCaret", "PanMilliseconds", "CaretMargin", "FocusFrame",
            "CaretMarker", "MouseFollow", "MouseMargin");
    }

    [Fact]
    public void Zoom_RoundTrip()
    {
        var back = ZoomMapping.Apply(new AppSettings(), ZoomMapping.Create(s_settings));

        // ZoomOn is left out on purpose: every start begins unzoomed, Create does not take it over.
        AssertSame(s_settings, back, "LastMode", "PerMonitorZoom", "AnimationMilliseconds", "LastZoomLevel", "MonitorZoomLevels",
            "LensZoomLevel", "LensMonitorZoomLevels", "DockedZoomLevel", "DockedMonitorZoomLevels", "LensWidth", "LensHeight",
            "DockEdge", "DockFraction");
    }

    [Fact]
    public void Pointer_UnreadableColour_FallsBackToTheDefault()
    {
        var style = PointerMapping.Style(new AppSettings() with { CursorColor = "not a colour" });

        Assert.Equal(PointerStyle.Default.Color, style.Color);
    }

    // Compares the named settings properties exactly (a misspelt name fails); dictionaries by content.
    private static void AssertSame(AppSettings expected, AppSettings actual, params string[] names)
    {
        foreach (var name in names)
        {
            var property = typeof(AppSettings).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"AppSettings has no property {name}.");
            var want = property.GetValue(expected);
            var got = property.GetValue(actual);
            if (want is IDictionary wantMap && got is IDictionary gotMap)
            {
                Assert.Equal(Entries(wantMap), Entries(gotMap));
                continue;
            }

            Assert.True(Equals(want, got), $"{property.Name}: expected {want}, got {got}");
        }
    }

    // The non-generic enumerator yields DictionaryEntry for every IDictionary, generic ones included.
    private static List<(object Key, object? Value)> Entries(IDictionary map)
    {
        var entries = new List<(object Key, object? Value)>();
        var enumerator = map.GetEnumerator();
        while (enumerator.MoveNext())
        {
            entries.Add((enumerator.Key, enumerator.Value));
        }

        return entries;
    }
}
