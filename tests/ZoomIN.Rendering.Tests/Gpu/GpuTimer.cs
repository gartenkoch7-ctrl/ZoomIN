using Vortice.Direct3D11;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Mean GPU time of a piece of work, from D3D11 timestamp queries.</summary>
internal static class GpuTimer
{
    public static double Measure(GpuDevice gpu, int frames, Action work)
    {
        using var disjoint = gpu.Device.CreateQuery(new QueryDescription(QueryType.TimestampDisjoint));
        using var start = gpu.Device.CreateQuery(new QueryDescription(QueryType.Timestamp));
        using var end = gpu.Device.CreateQuery(new QueryDescription(QueryType.Timestamp));

        work(); // warm-up
        var total = 0.0;
        var counted = 0;
        for (var i = 0; i < frames; i++)
        {
            gpu.Context.Begin(disjoint);
            gpu.Context.End(start);
            work();
            gpu.Context.End(end);
            gpu.Context.End(disjoint);

            QueryDataTimestampDisjoint info;
            while (!gpu.Context.GetData(disjoint, out info))
            {
                Thread.Yield();
            }

            ulong begin, finish;
            while (!gpu.Context.GetData(start, out begin))
            {
                Thread.Yield();
            }

            while (!gpu.Context.GetData(end, out finish))
            {
                Thread.Yield();
            }

            if (!info.Disjoint)
            {
                total += (finish - begin) * 1000.0 / info.Frequency;
                counted++;
            }
        }

        return counted == 0 ? double.NaN : total / counted;
    }
}
