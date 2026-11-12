using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Still;

namespace ZoomIN.Rendering.Islands;

/// <summary>
/// The still images of one monitor's islands (M19 B1): one <see cref="StillFrame"/> per place,
/// taken once when an island switches to "still" or its source changes, and freed when the island
/// is gone or live again. Runs every frame on the render thread: without a change it only compares
/// a few numbers (0 B); a capture copies on the GPU and creates a texture only for a new size.
/// </summary>
internal sealed class IslandStills : IDisposable
{
    private readonly StillFrame[] _frames = new StillFrame[IslandRules.MaxCount];

    // Per place: id of the island it holds (0 = free) and the source it was taken from.
    private readonly int[] _ids = new int[IslandRules.MaxCount];
    private readonly PhysicalRect[] _sources = new PhysicalRect[IslandRules.MaxCount];

    public IslandStills(ID3D11Device device, ILogger log)
    {
        for (var i = 0; i < _frames.Length; i++)
        {
            _frames[i] = new StillFrame(device, log);
        }
    }

    /// <summary>
    /// Takes missing images from <paramref name="desktop"/> (the monitor's captured desktop) and
    /// frees images of islands that are gone or live.
    /// </summary>
    public void Update(ID3D11DeviceContext context, ID3D11Texture2D desktop, IslandSet set, string monitorId, PhysicalRect monitor)
    {
        for (var place = 0; place < _ids.Length; place++)
        {
            if (_ids[place] != 0 && !IsStillHere(set, _ids[place], monitorId))
            {
                _frames[place].Release();
                _ids[place] = 0;
            }
        }

        for (var i = 0; i < set.Count; i++)
        {
            var island = set[i];
            if (!island.Still || !string.Equals(island.MonitorId, monitorId, StringComparison.Ordinal))
            {
                continue;
            }

            var place = PlaceOf(island.Id);
            if (place < 0)
            {
                place = PlaceOf(0);
            }

            // Four islands fill four places; a free one is always there.
            if (place < 0 || (_ids[place] == island.Id && _sources[place] == island.Source && _frames[place].HasImage))
            {
                continue;
            }

            var region = new PhysicalRect(island.Source.X - monitor.X, island.Source.Y - monitor.Y, island.Source.Width, island.Source.Height);
            if (_frames[place].Capture(context, desktop, region))
            {
                _ids[place] = island.Id;
                _sources[place] = island.Source;
            }
        }
    }

    /// <summary>The still image of the island, or null (live, or not taken yet).</summary>
    public StillFrame? For(int islandId)
    {
        var place = PlaceOf(islandId);
        return place >= 0 && _frames[place].HasImage ? _frames[place] : null;
    }

    public void Dispose()
    {
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
    }

    private static bool IsStillHere(IslandSet set, int id, string monitorId)
    {
        var index = set.IndexOf(id);
        return index >= 0 && set[index].Still && string.Equals(set[index].MonitorId, monitorId, StringComparison.Ordinal);
    }

    private int PlaceOf(int id)
    {
        for (var place = 0; place < _ids.Length; place++)
        {
            if (_ids[place] == id)
            {
                return place;
            }
        }

        return -1;
    }
}
