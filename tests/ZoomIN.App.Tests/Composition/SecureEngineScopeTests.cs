using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZoomIN.App.Composition;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.App.Tests.Composition;

/// <summary>M10 Abschluss: the secure desktop runs only the core engine; the tray app keeps everything.</summary>
public sealed class SecureEngineScopeTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ZoomIN-ScopeTests-").FullName;
    private readonly StartLog _log = new();
    private readonly RecordingHotkeys _hotkeys = new();
    private readonly RecordingFence _fence = new();

    private TelemetryHost? _telemetry;

    public void Dispose()
    {
        _telemetry?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void SecureDesktop_StartsOnlyTheCore()
    {
        using var provider = Build(EngineScope.SecureDesktop);

        provider.GetRequiredService<ZoomController>().Initialize();

        Assert.Contains("output", _log.Started);
        Assert.Contains("wheel", _log.Started);
        Assert.Contains("tracking", _log.Started);
        Assert.DoesNotContain("windowdrag", _log.Started);
        Assert.DoesNotContain(HotkeyGesture.ToggleIslands, _hotkeys.Registered);
        Assert.DoesNotContain(HotkeyGesture.EditIslands, _hotkeys.Registered);
        Assert.DoesNotContain(HotkeyGesture.FreezeMonitor, _hotkeys.Registered);
        Assert.DoesNotContain(HotkeyGesture.NextMonitor, _hotkeys.Registered);
        Assert.Contains(HotkeyGesture.ToggleZoom, _hotkeys.Registered);
        Assert.Empty(provider.GetRequiredService<HotkeyBindings>().Unavailable);
    }

    [Fact]
    public void SecureDesktop_HasNoIslands_AndNobodyOwnsTheFence()
    {
        using var provider = Build(EngineScope.SecureDesktop);

        provider.GetRequiredService<ZoomController>().Initialize();

        var scene = provider.GetRequiredService<IslandModel>().Scene;
        Assert.Equal(0, scene.Set.Count);
        Assert.False(scene.Visible);
        Assert.Empty(_fence.Owners);
        Assert.Empty(_fence.Filters);
    }

    [Fact]
    public void TrayApp_StillStartsEverything()
    {
        using var provider = Build(EngineScope.Full);

        provider.GetRequiredService<ZoomController>().Initialize();

        Assert.Contains("windowdrag", _log.Started);
        Assert.Contains(HotkeyGesture.ToggleIslands, _hotkeys.Registered);
        Assert.Contains(HotkeyGesture.FreezeMonitor, _hotkeys.Registered);
        Assert.Equal(1, provider.GetRequiredService<IslandModel>().Scene.Set.Count);
    }

    private ServiceProvider Build(EngineScope scope)
    {
        var folders = new ScopeFolders(_folder);
        var telemetry = _telemetry = TelemetryHost.Start(TelemetryRole.App, folders);
        var services = scope == EngineScope.Full
            ? ServiceRegistration.Tray(telemetry, folders)
            : ServiceRegistration.SecureDesktop(telemetry, folders);
        var settings = new AppSettings
        {
            Islands = new IslandOptions
            {
                Visible = true,
                Items = [new IslandEntry { MonitorId = @"\\.\DISPLAY1", SourceWidth = 200, SourceHeight = 100, TargetX = 10, TargetY = 10 }],
            },
        };
        services.Replace(ServiceDescriptor.Singleton<ISettingsStore>(new FixedSettingsStore(settings)));
        services.Replace(ServiceDescriptor.Singleton<IProfileStore>(new EmptyProfileStore()));
        services.Replace(ServiceDescriptor.Singleton<IHotkeyService>(_hotkeys));
        services.Replace(ServiceDescriptor.Singleton<IPointerFence>(_fence));
        services.Replace(ServiceDescriptor.Singleton<IMouseWheelHook>(new StartingWheel(_log)));
        services.Replace(ServiceDescriptor.Singleton<IInputActivitySource>(new StartingActivity(_log)));
        services.Replace(ServiceDescriptor.Singleton<ITrackingSource>(new StartingTracking(_log)));
        services.Replace(ServiceDescriptor.Singleton<IHoldKeySource>(new StartingHoldKey(_log)));
        services.Replace(ServiceDescriptor.Singleton<IWindowDragSource>(new StartingWindowDrag(_log)));
        services.Replace(ServiceDescriptor.Singleton<IWindowFilterSource>(new StartingWindowFilter(_log)));
        services.Replace(ServiceDescriptor.Singleton<IZoomOutput>(new StartingOutput(_log)));
        return services.BuildServiceProvider();
    }
}
