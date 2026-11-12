using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Benchmarks;

/// <summary>
/// What the render loop and the mouse hook do per frame or move for the islands (M19 B1): read the
/// scene, decide whether a monitor draws islands, walk four islands and their pictures; confine a
/// pointer move during a drag. Budget: 0 B allocated. The GPU draw itself is covered on WARP by
/// IslandRenderPathTests (also 0 B).
/// </summary>
public class IslandBenchmarks
{
    private const string Monitor = @"\\.\DISPLAY1";
    private readonly IslandModel _model = new(Four(), visible: true);
    private readonly IslandDragFence _fence = new();
    private int _frame;

    [GlobalSetup]
    public void Setup() => _fence.Confine(new PhysicalRect(0, 0, 3840, 2160));

    [Benchmark]
    public double IslandFrame()
    {
        _frame++;
        var scene = _model.Scene;
        if (!scene.ShowsOn(Monitor, fullscreenZoomed: (_frame & 1) == 0))
        {
            return 0;
        }

        var sum = 0.0;
        var set = scene.Set;
        for (var i = 0; i < set.Count; i++)
        {
            var island = set[i];
            if (string.Equals(island.MonitorId, Monitor, StringComparison.Ordinal))
            {
                var picture = island.Target;
                sum += picture.X + picture.Right + island.Source.Width;
            }
        }

        return sum;
    }

    [Benchmark]
    public double IslandDragMove() => _fence.Filter(_frame, default, new PhysicalPoint(++_frame % 5000, 100)).X;

    private static IslandSet Four()
    {
        var set = IslandSet.Empty;
        for (var i = 0; i < IslandRules.MaxCount; i++)
        {
            set = set.Add(new Island(0, Monitor, new PhysicalRect(i * 400, 2100, 200, 60), new PhysicalPoint(i * 900, 0), 3))!;
        }

        return set;
    }
}
