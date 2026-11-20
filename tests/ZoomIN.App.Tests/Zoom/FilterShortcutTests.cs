using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.Tests.Zoom;

/// <summary>ADR-0033: the colour filter shortcut (Ctrl+Alt+C by default) on a smart monitor switches the active window; twice quickly, the monitor.</summary>
public sealed class FilterShortcutTests
{
    private const string Monitor = @"\\.\DISPLAY1";
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");

    [Fact]
    public void OnASmartMonitor_OnePress_SwitchesOnlyTheActiveWindow()
    {
        var rig = new Rig(FilterId.InvertedSmart);

        rig.Shortcut.Press();

        Assert.Equal("window", rig.Shortcut.LastTarget);
        Assert.True(rig.Effects.CurrentFor(Monitor).Filter.Enabled); // the monitor stays smart
        Assert.Equal(WindowFilterChoice.None, rig.Windows.Options.RuleFor(s_word)!.Choice);
    }

    [Fact]
    public void ThePressAfterThat_GivesTheWindowBackToTheSmartFilter()
    {
        var rig = new Rig(FilterId.InvertedSmart);
        rig.Shortcut.Press();
        rig.Time.Advance(TimeSpan.FromSeconds(2));
        rig.Source.Palette = WindowPalette.Original; // what the tracker reports after the first rule

        rig.Shortcut.Press();

        Assert.Equal("window", rig.Shortcut.LastTarget);
        Assert.Equal(WindowFilterChoice.Automatic, rig.Windows.Options.RuleFor(s_word)!.Choice);
    }

    [Fact]
    public void TwoQuickPresses_SwitchTheWholeMonitor_AndUndoTheWindowRule()
    {
        var rig = new Rig(FilterId.InvertedSmart);

        rig.Shortcut.Press();
        rig.Time.Advance(TimeSpan.FromMilliseconds(200));
        rig.Shortcut.Press();

        Assert.Equal("double", rig.Shortcut.LastTarget);
        Assert.False(rig.Effects.CurrentFor(Monitor).Filter.Enabled);
        Assert.Null(rig.Windows.Options.RuleFor(s_word));
    }

    [Fact]
    public void AnotherFilter_SwitchesTheMonitorAsBefore_AlsoTwice()
    {
        var rig = new Rig(FilterId.Inverted);

        rig.Shortcut.Press();
        Assert.Equal("monitor", rig.Shortcut.LastTarget);
        Assert.False(rig.Effects.CurrentFor(Monitor).Filter.Enabled);

        rig.Time.Advance(TimeSpan.FromMilliseconds(100));
        rig.Shortcut.Press();
        Assert.True(rig.Effects.CurrentFor(Monitor).Filter.Enabled);
        Assert.Null(rig.Windows.Options.RuleFor(s_word));
    }

    [Fact]
    public void WithoutTheFilterPerWindow_TheMonitorIsSwitched()
    {
        var rig = new Rig(FilterId.InvertedSmart, windowFilter: false);

        rig.Shortcut.Press();

        Assert.Equal("monitor", rig.Shortcut.LastTarget);
        Assert.False(rig.Effects.CurrentFor(Monitor).Filter.Enabled);
    }

    [Fact]
    public void TheMousePosition_GoesToTheSource_ForTheDesktopCase()
    {
        var rig = new Rig(FilterId.InvertedSmart);

        rig.Shortcut.Press();

        Assert.Equal(new PhysicalPoint(100, 100), rig.Source.Mouse); // TrackerSnapshotTests cover the choice
    }

    [Fact]
    public void ARuleOfTheShortcut_DecidesTheNextPress_EvenBeforeTheTrackerCaughtUp()
    {
        var rig = new Rig(FilterId.InvertedSmart);
        rig.Shortcut.Press();
        rig.Time.Advance(TimeSpan.FromMilliseconds(500)); // palette still "monitor": not yet re-published

        rig.Shortcut.Press();

        Assert.Equal(WindowFilterChoice.Automatic, rig.Windows.Options.RuleFor(s_word)!.Choice);
    }

    private sealed class Rig
    {
        public Rig(FilterId filter, bool windowFilter = true)
        {
            Effects = new EffectsModel(DisplayEffects.Default with { Filter = new FilterSettings(true, filter, 1, 0) }, Time);
            Windows = new WindowFilterModel(WindowFilterOptions.Default with { Enabled = windowFilter });
            var monitors = new OneMonitor();
            var commands = new WindowFilterCommands(Windows, Effects, Source, monitors);
            Shortcut = new FilterShortcut(Effects, Windows, commands, monitors, new Mouse(), Time);
        }

        public ManualTime Time { get; } = new();

        public EffectsModel Effects { get; }

        public WindowFilterModel Windows { get; }

        public OneWindow Source { get; } = new();

        public FilterShortcut Shortcut { get; }
    }

    internal sealed class ManualTime : TimeProvider
    {
        private long _ticks = 1_000_000;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    internal sealed class OneWindow : IWindowFilterSource
    {
        public byte Palette { get; set; } = WindowPalette.Monitor;

        public PhysicalPoint? Mouse { get; private set; }

        public void Start(WindowFilterModel model)
        {
        }

        public bool TryGetForeground(out WindowFilterTarget window)
        {
            window = Target();
            return true;
        }

        public bool TryGetActive(PhysicalPoint? mouse, out WindowFilterTarget window)
        {
            Mouse = mouse;
            window = Target();
            return true;
        }

        public IReadOnlyList<WindowKind> OpenKinds() => [s_word];

        public void Dispose()
        {
        }

        private WindowFilterTarget Target() => new(s_word, Palette, new PhysicalRect(0, 0, 800, 600));
    }

    private sealed class OneMonitor : IMonitorProvider
    {
        public IReadOnlyList<string> MonitorIds() => [Monitor];

        public string MonitorAt(PhysicalPoint point) => Monitor;

        public PhysicalRect? BoundsOf(string monitorId) => new PhysicalRect(0, 0, 1920, 1080);
    }

    private sealed class Mouse : ICursorSource
    {
        public bool TryGetPosition(out PhysicalPoint position)
        {
            position = new PhysicalPoint(100, 100);
            return true;
        }
    }
}
