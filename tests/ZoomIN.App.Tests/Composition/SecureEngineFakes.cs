using ZoomIN.Core.Folders;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Tests.Composition;

/// <summary>Records which engine parts the secure-desktop container starts (SecureEngineScopeTests).</summary>
internal sealed class StartLog
{
    public List<string> Started { get; } = [];
}

internal sealed record ScopeFolders(string Root) : IAppFolders
{
    public string Local => Root;

    public string Roaming => Path.Combine(Root, "roaming");
}

internal sealed class FixedSettingsStore(AppSettings settings) : ISettingsStore
{
    public AppSettings Load() => settings;

    public void Save(AppSettings value)
    {
    }
}

internal sealed class EmptyProfileStore : IProfileStore
{
    public IReadOnlyList<string> Names() => [];

    public AppSettings? Load(string name) => null;

    public void Save(string name, AppSettings settings)
    {
    }

    public void Delete(string name)
    {
    }

    public bool Rename(string oldName, string newName) => false;

    public void Export(string name, string filePath)
    {
    }

    public string? Import(string filePath) => null;
}

internal sealed class RecordingHotkeys : IHotkeyService
{
    public List<HotkeyGesture> Registered { get; } = [];

    public bool Register(HotkeyGesture gesture, Action onPressed, bool repeat = false)
    {
        Registered.Add(gesture);
        return true;
    }

    public void UnregisterAll() => Registered.Clear();

    public void Dispose()
    {
    }
}

internal sealed class RecordingFence : IPointerFence
{
    public event EventHandler<FenceTrigger>? Recomputed;

    public List<FenceOwner> Owners { get; } = [];

    public List<IPointerMoveFilter> Filters { get; } = [];

    public FenceMode Mode => FenceMode.Off;

    public void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas)
    {
        if (areas.Count > 0)
        {
            Owners.Add(owner);
        }
    }

    public void AddFilter(IPointerMoveFilter filter) => Filters.Add(filter);

    public void RemoveFilter(IPointerMoveFilter filter) => Filters.Remove(filter);

    public void Dispose() => Recomputed?.Invoke(this, default);
}

internal sealed class StartingWheel(StartLog log) : IMouseWheelHook
{
    public void Start(HotkeyModifiers modifiers, Action<double> onNotches) => log.Started.Add("wheel");

    public void Dispose()
    {
    }
}

internal sealed class StartingActivity(StartLog log) : IInputActivitySource
{
    public void Start(IInputActivityHandler handler) => log.Started.Add("activity");

    public void Dispose()
    {
    }
}

internal sealed class StartingTracking(StartLog log) : ITrackingSource
{
    public void Start(TrackingModel model) => log.Started.Add("tracking");

    public void Dispose()
    {
    }
}

internal sealed class StartingHoldKey(StartLog log) : IHoldKeySource
{
    public bool Start(IHoldKeyHandler handler)
    {
        log.Started.Add("holdkey");
        return true;
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class StartingWindowDrag(StartLog log) : IWindowDragSource
{
    public void Start(Action<WindowDragSignal> signal, Func<bool> active) => log.Started.Add("windowdrag");

    public void Dispose()
    {
    }
}

internal sealed class StartingWindowFilter(StartLog log) : IWindowFilterSource
{
    public void Start(WindowFilterModel model) => log.Started.Add("windowfilter");

    public bool TryGetForeground(out WindowFilterTarget window)
    {
        window = default;
        return false;
    }

    public IReadOnlyList<WindowKind> OpenKinds() => [];

    public void Dispose()
    {
    }
}

internal sealed class StartingOutput(StartLog log) : IZoomOutput
{
    public ZoomStatus Status => ZoomStatus.Idle;

    public void Start() => log.Started.Add("output");

    public void Dispose()
    {
    }
}
