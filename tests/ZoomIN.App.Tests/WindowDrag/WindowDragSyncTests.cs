using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.WindowDrag;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Tests.WindowDrag;

/// <summary>Drag steps from the source reach the model on the monitor under the pointer (G1).</summary>
public sealed class WindowDragSyncTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";

    [Fact]
    public void Drag_LiftsTheMonitorUnderThePointer_FollowsIt_AndGivesItBack()
    {
        var (sync, model, source, monitors) = Create();
        sync.Start();
        Assert.True(source.Active!()); // lift is on: windows are probed

        source.Send(new WindowDragSignal(WindowDragStage.Started, WindowDragKind.Move));
        Assert.Equal(1, model.EffectiveTargetFor(Left));

        monitors.Pointer = Right;
        source.Send(new WindowDragSignal(WindowDragStage.MonitorChanged));
        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(1, model.EffectiveTargetFor(Right));

        source.Send(new WindowDragSignal(WindowDragStage.Ended));
        Assert.Equal(4, model.EffectiveTargetFor(Right));
    }

    [Fact]
    public void UnknownMonitor_MeansANewMonitorSet_AndGivesTheLevelBack()
    {
        var (sync, model, source, monitors) = Create();
        sync.Start();
        source.Send(new WindowDragSignal(WindowDragStage.Started, WindowDragKind.Resize));

        monitors.Pointer = @"\\.\DISPLAY7";
        source.Send(new WindowDragSignal(WindowDragStage.MonitorChanged));

        Assert.False(model.Temporary.IsActive(Left));
        source.Send(new WindowDragSignal(WindowDragStage.Ended));
        source.Send(new WindowDragSignal(WindowDragStage.Ended)); // a second end does nothing
    }

    [Fact]
    public void FailingStep_IsLogged_NotThrownIntoTheSource()
    {
        var (sync, _, source, monitors) = Create();
        sync.Start();
        monitors.Fail = true;

        var exception = Record.Exception(() => source.Send(new WindowDragSignal(WindowDragStage.Started, WindowDragKind.Move)));

        Assert.Null(exception);
    }

    [Fact]
    public void WithoutThePointer_TheDragIsLeftAlone()
    {
        var cursor = new FakeCursor { Available = false };
        var (sync, model, source, _) = Create(cursor);
        sync.Start();

        source.Send(new WindowDragSignal(WindowDragStage.Started, WindowDragKind.Move));
        source.Send(new WindowDragSignal(WindowDragStage.MonitorChanged));

        Assert.False(model.Temporary.IsActive(Left));
        source.Send(new WindowDragSignal(WindowDragStage.Ended));
    }

    private static (WindowDragSync Sync, ZoomModel Model, FakeSource Source, FakeMonitors Monitors) Create(FakeCursor? cursor = null)
    {
        var levels = new ZoomLevels();
        levels.Set(ZoomMode.Fullscreen, Left, 4, perMonitor: true);
        levels.Set(ZoomMode.Fullscreen, Right, 4, perMonitor: true);
        var model = new ZoomModel(levels, ZoomMode.Fullscreen, perMonitor: true, 0.15);
        model.Toggle(Left, [Left, Right]);
        var source = new FakeSource();
        var monitors = new FakeMonitors();
        var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        var sync = new WindowDragSync(drag, source, monitors, cursor ?? new FakeCursor(), NullLogger<WindowDragSync>.Instance);
        return (sync, model, source, monitors);
    }

    private sealed class FakeSource : IWindowDragSource
    {
        private Action<WindowDragSignal>? _signal;

        public void Start(Action<WindowDragSignal> signal, Func<bool> active) => (_signal, Active) = (signal, active);

        public Func<bool>? Active { get; private set; }

        public void Send(WindowDragSignal signal) => _signal!(signal);

        public void Dispose()
        {
        }
    }

    private sealed class FakeMonitors : IMonitorProvider
    {
        public string Pointer { get; set; } = Left;

        public bool Fail { get; set; }

        public IReadOnlyList<string> MonitorIds() => Fail ? throw new InvalidOperationException("gone") : [Left, Right];

        public string MonitorAt(PhysicalPoint point) => Pointer;

        public PhysicalRect? BoundsOf(string monitorId) => null;
    }

    private sealed class FakeCursor : ICursorSource
    {
        public bool Available { get; set; } = true;

        public bool TryGetPosition(out PhysicalPoint position)
        {
            position = default;
            return Available;
        }
    }
}
