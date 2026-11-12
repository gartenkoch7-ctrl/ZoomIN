using System.Diagnostics;
using Vortice.Direct3D11;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Islands;

/// <summary>
/// Draws the islands of one monitor above the zoom (M19 B1): per island one draw of the magnify
/// pass with its source, picture and frame, so colour filter, filter per window, own schemes,
/// smart inversion, smoothing and "Schärfe"/"Fett" act as on the main picture. Still islands draw
/// their still image instead of the live desktop. In the edit mode the frames blink and the
/// selected island has corner handles; a new one shows a dashed rubber band. All in the existing
/// overlay window, which is excluded from capture. Every frame on the render thread: no allocation.
/// </summary>
internal sealed class IslandLayer : IDisposable
{
    // Frame around each island, in physical pixels (a little thinner than the lens frame).
    private const double FrameThickness = 3;

    private static readonly FrameColor s_blink = new(1, 1, 1, 1);

    private readonly MagnifyPass _pass;
    private readonly IslandEditMarks _marks;
    private readonly IslandStills _stills;
    private readonly GpuPassTimer _time;
    private int _phase = -1;
    private int _drawnPhase = -1;

    public IslandLayer(MagnifyPass pass, CursorPass marks, IslandStills stills, GpuPassTimer time)
    {
        _pass = pass;
        _marks = new IslandEditMarks(marks);
        _stills = stills;
        _time = time;
    }

    /// <summary>The edit mode's frames changed colour since they were last drawn.</summary>
    public bool NeedsRedraw => _phase != _drawnPhase;

    /// <summary>Call every frame before the change check: the blink of the edit mode.</summary>
    public void Update(IslandScene? scene, long now)
    {
        _phase = scene?.Edit is { } edit
            ? (int)(Stopwatch.GetElapsedTime(edit.StartedAt, now).TotalSeconds / IslandEditView.BlinkSeconds) & 1
            : -1;
    }

    /// <param name="desktop">The captured desktop of the monitor, for live islands.</param>
    /// <param name="desktopTexture">The same as a texture, for still images.</param>
    /// <param name="weights">Smart inversion weights of this frame, or null.</param>
    public void Draw(
        ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, ID3D11Texture2D desktopTexture, ID3D11RenderTargetView target,
        string monitorId, PhysicalRect monitor, IslandScene? scene, DisplayEffects effects, ID3D11ShaderResourceView? weights)
    {
        _drawnPhase = _phase;
        if (scene is null)
        {
            return;
        }

        var set = scene.Set;
        _stills.Update(context, desktopTexture, set, monitorId, monitor);
        _time.Collect(context);
        _time.Begin(context);
        var frame = _phase == 1 ? s_blink : FrameColor.Default;
        var smart = effects.IsSmart && weights is not null;
        for (var i = 0; i < set.Count; i++)
        {
            var island = set[i];
            if (!string.Equals(island.MonitorId, monitorId, StringComparison.Ordinal) || !Inside(island, monitor))
            {
                continue; // monitor changed since the island was made: hidden until it fits again
            }

            var still = island.Still ? _stills.For(island.Id) : null;
            var picture = island.Target;
            var viewport = Expand(picture, FrameThickness, monitor);
            var constants = MagnifyConstants.ForIsland(
                island.Source, monitor, picture, viewport, frame, effects.Matrix, effects.Smoothing, smart,
                effects.Enhancement.At(island.Level, effects.Smoothing),
                still is null ? StillMap.Live : StillMap.For(still.Source, monitor.Width, monitor.Height));
            _pass.Draw(context, still?.ShaderResource ?? desktop, target, (float)(viewport.X - monitor.X), (float)(viewport.Y - monitor.Y),
                (float)viewport.Width, (float)viewport.Height, constants, weights);
        }

        _time.End(context);
        if (scene.Edit is { } edit)
        {
            DrawEditMarks(context, target, monitorId, monitor, set, edit, frame);
        }
    }

    public void Dispose()
    {
        _time.Dispose();
        _stills.Dispose();
    }

    private void DrawEditMarks(
        ID3D11DeviceContext context, ID3D11RenderTargetView target, string monitorId, PhysicalRect monitor, IslandSet set,
        IslandEditView edit, FrameColor color)
    {
        try
        {
            if (edit.Selected >= 0 && edit.Selected < set.Count
                && string.Equals(set[edit.Selected].MonitorId, monitorId, StringComparison.Ordinal))
            {
                _marks.DrawHandles(context, target, monitor, set[edit.Selected].Target, color);
            }

            if (edit.RubberBand is { } band)
            {
                _marks.DrawRubberBand(context, target, monitor, band);
            }
        }
        finally
        {
            _marks.End(context);
        }
    }

    // Half a pixel of tolerance: Fit rounds to whole pixels, the monitor may have a fractional origin.
    private static bool Inside(Island island, PhysicalRect monitor)
    {
        var picture = island.Target;
        var source = island.Source;
        return source.Width > 0 && source.Height > 0
            && source.X >= monitor.X - 0.5 && source.Right <= monitor.Right + 0.5
            && source.Y >= monitor.Y - 0.5 && source.Bottom <= monitor.Bottom + 0.5
            && picture.X >= monitor.X - 0.5 && picture.Right <= monitor.Right + 0.5
            && picture.Y >= monitor.Y - 0.5 && picture.Bottom <= monitor.Bottom + 0.5;
    }

    private static PhysicalRect Expand(PhysicalRect rect, double by, PhysicalRect within)
    {
        var x = Math.Max(rect.X - by, within.X);
        var y = Math.Max(rect.Y - by, within.Y);
        return new PhysicalRect(x, y, Math.Min(rect.Right + by, within.Right) - x, Math.Min(rect.Bottom + by, within.Bottom) - y);
    }
}
