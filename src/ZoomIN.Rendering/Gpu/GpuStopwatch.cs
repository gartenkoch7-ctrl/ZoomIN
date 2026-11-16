using Vortice.Direct3D11;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// GPU time of one piece of work per frame from timestamp queries, read a few frames later so the
/// render thread never waits for the GPU. Queries are created once; <see cref="Begin"/>,
/// <see cref="End"/> and <see cref="TryRead"/> do not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class GpuStopwatch : IDisposable
{
    // The GPU is at most a few frames behind; with three sets the oldest is usually done.
    private const int Sets = 3;

    private readonly ID3D11Query[] _disjoint = new ID3D11Query[Sets];
    private readonly ID3D11Query[] _start = new ID3D11Query[Sets];
    private readonly ID3D11Query[] _end = new ID3D11Query[Sets];
    private readonly bool[] _pending = new bool[Sets];
    private int _next;

    public GpuStopwatch(ID3D11Device device)
    {
        for (var i = 0; i < Sets; i++)
        {
            _disjoint[i] = device.CreateQuery(new QueryDescription(QueryType.TimestampDisjoint));
            _start[i] = device.CreateQuery(new QueryDescription(QueryType.Timestamp));
            _end[i] = device.CreateQuery(new QueryDescription(QueryType.Timestamp));
        }
    }

    /// <summary>Starts timing; false if every set is still waiting for the GPU (then skip this frame).</summary>
    public bool Begin(ID3D11DeviceContext context)
    {
        if (_pending[_next])
        {
            return false;
        }

        context.Begin(_disjoint[_next]);
        context.End(_start[_next]);
        return true;
    }

    public void End(ID3D11DeviceContext context)
    {
        context.End(_end[_next]);
        context.End(_disjoint[_next]);
        _pending[_next] = true;
        _next = (_next + 1) % Sets;
    }

    /// <summary>One finished measurement in milliseconds, if the GPU has one ready.</summary>
    public bool TryRead(ID3D11DeviceContext context, out double milliseconds)
    {
        milliseconds = 0;
        for (var n = 0; n < Sets; n++)
        {
            var i = (_next + n) % Sets;
            if (!_pending[i]
                || !context.GetData(_disjoint[i], AsyncGetDataFlags.DoNotFlush, out QueryDataTimestampDisjoint info)
                || !context.GetData(_start[i], AsyncGetDataFlags.DoNotFlush, out ulong begin)
                || !context.GetData(_end[i], AsyncGetDataFlags.DoNotFlush, out ulong end))
            {
                continue;
            }

            _pending[i] = false;
            if (!info.Disjoint && info.Frequency > 0 && end >= begin)
            {
                milliseconds = (end - begin) * 1000.0 / info.Frequency;
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        for (var i = 0; i < Sets; i++)
        {
            _end[i].Dispose();
            _start[i].Dispose();
            _disjoint[i].Dispose();
        }
    }
}
