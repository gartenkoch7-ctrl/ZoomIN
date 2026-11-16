using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Benchmarks;

/// <summary>
/// Start timings of the secure instance (M10): every render pass checks the milestones until all
/// three happened. Budget: 0 B allocated.
/// </summary>
public class MilestoneBenchmarks
{
    private readonly RenderMilestones _pending = new(TimeProvider.System);
    private readonly RenderMilestones _complete = new(TimeProvider.System);

    [GlobalSetup]
    public void Setup()
    {
        _pending.Arm();
        _complete.Arm();
        _complete.Observe(captured: true, shown: true, zoomReached: true);
    }

    /// <summary>Zoom off with a filter on: the zoom level is never reached, so every frame observes.</summary>
    [Benchmark]
    public bool ObserveWhilePending()
    {
        if (!_pending.IsComplete)
        {
            _pending.Observe(captured: true, shown: true, zoomReached: false);
        }

        return _pending.IsComplete;
    }

    /// <summary>The usual case after the start: one check per frame.</summary>
    [Benchmark]
    public bool CheckWhenComplete() => _complete.IsComplete;
}
