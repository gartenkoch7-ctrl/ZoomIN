using Vortice.DXGI;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.Capture;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Runs one <see cref="MonitorRenderer"/> per attached monitor (ADR-0010) and rebuilds them all
/// when the display configuration changes. Pipelines stay warm while zoom is off (M2 decision).
/// </summary>
public sealed class ZoomEngine : IZoomOutput
{
    // Windows sends several display messages per change; wait until the layout has settled.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);

    // Render watchdog (M8): a renderer without progress for this long is considered hung.
    private static readonly TimeSpan HangLimit = TimeSpan.FromSeconds(2);
    private readonly Timer _watchdog;

    private readonly RenderModels _models;
    private readonly ZoomModel _model;
    private readonly EffectsModel _effects;
    private readonly PointerServices _pointer;
    private readonly ILogger<ZoomEngine> _log;
    private readonly Lock _gate = new();
    private MonitorRenderer[] _renderers = [];
    private int _rebuildPending;
    private bool _started;
    private bool _disposed;

    public ZoomEngine(RenderModels models, PointerServices pointerServices, ILogger<ZoomEngine> log)
    {
        _models = models;
        _model = models.Zoom;
        _effects = models.Effects;
        _pointer = pointerServices;
        _log = log;
        _model.Changed += Wake;
        _model.Temporary.Changed += Wake;
        _effects.Changed += Wake;
        _effects.Signaled += Wake;
        _models.Windows.RegionsChanged += Wake;
        _watchdog = new Timer(_ => CheckForHangs(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public ZoomStatus Status
    {
        get
        {
            var statuses = Volatile.Read(ref _renderers).Select(r => r.Status).ToList();
            var visible = statuses.Where(s => s.IsVisible).ToList();
            return new ZoomStatus(
                visible.Count > 0,
                statuses.Count > 0 && statuses.All(s => s.IsCaptureExcluded),
                visible.Count > 0 ? visible.Min(s => s.FramesPerSecond) : 0,
                statuses.Select(s => s.LastError).FirstOrDefault(e => e is not null))
            { MonitorCount = statuses.Count };
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            StartRenderers();
        }
    }

    public void Dispose()
    {
        _watchdog.Dispose();
        _model.Changed -= Wake;
        _model.Temporary.Changed -= Wake;
        _effects.Changed -= Wake;
        _effects.Signaled -= Wake;
        _models.Windows.RegionsChanged -= Wake;
        lock (_gate)
        {
            _disposed = true;
            StopRenderers();
        }
    }

    private void Wake()
    {
        foreach (var renderer in Volatile.Read(ref _renderers))
        {
            renderer.Wake();
        }
    }

    /// <summary>Called from a render thread; the rebuild runs elsewhere so that thread can end.</summary>
    private void RequestRebuild()
    {
        if (Interlocked.Exchange(ref _rebuildPending, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(SettleDelay).ConfigureAwait(false);
            lock (_gate)
            {
                Volatile.Write(ref _rebuildPending, 0);
                if (_disposed)
                {
                    return;
                }

                StopRenderers();
                StartRenderers();
            }
        });
    }

    private void StartRenderers()
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.render.rebuild");
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        var names = MonitorOutput.AttachedDeviceNames(factory);
        _log.Monitors(names);

        var renderers = names.Select(name => new MonitorRenderer(name, _models, _pointer, _log, RequestRebuild)).ToArray();
        operation?.SetTag("zoomin.monitors", renderers.Length);
        foreach (var renderer in renderers)
        {
            renderer.Start();
        }

        Volatile.Write(ref _renderers, renderers);
    }

    private void StopRenderers()
    {
        var renderers = Volatile.Read(ref _renderers);
        Volatile.Write(ref _renderers, []);
        foreach (var renderer in renderers)
        {
            renderer.Dispose();
            if (renderer.IsAlive && !_disposed)
            {
                // A thread stuck in the driver cannot be stopped, and its overlay may still cover
                // the screen. End the process; the watchdog cleans up and restarts ZoomIN (ADR-0007).
                _log.RenderThreadStuck();
                Telemetry.FlushBeforeExit();
                Environment.FailFast("ZoomIN render thread hung");
            }
        }
    }

    private void CheckForHangs()
    {
        foreach (var renderer in Volatile.Read(ref _renderers))
        {
            if (renderer.IsHung(HangLimit))
            {
                _log.RenderThreadHung(HangLimit.TotalSeconds);
                RequestRebuild();
                return;
            }
        }
    }
}
