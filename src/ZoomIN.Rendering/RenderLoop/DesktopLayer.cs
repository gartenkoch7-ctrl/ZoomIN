using Vortice.Direct3D11;
using Vortice.Mathematics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Draws the captured desktop of one monitor with zoom, colour filter, filter per window and smart
/// inversion into the overlay. <see cref="Draw"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class DesktopLayer(MagnifyPass pass, SmartInvertLayer smart, GpuPassTimer magnifyTime, SmartCostProbe probe)
{
    // Frame around lens and docked view, in physical pixels.
    private const double FrameThickness = 4;
    private static readonly Color4 s_transparent = new(0, 0, 0, 0);

    // Window regions as last uploaded; compared by reference, both are immutable snapshots.
    private WindowConstants _windowConstants;
    private WindowRegions? _uploadedWindows;
    private DisplayEffects? _uploadedEffects;
    private bool _uploadedSmart;

    // Window constants without smart entries, for the plain draw of the cost probe (M14 step 5).
    private WindowConstants _probeConstants;
    private bool _probedMonitorSmart;

    /// <summary>The smart inversion smoothing still moves: keep drawing frames (M14 step 3).</summary>
    public bool SmartSettling => smart.Settling;

    /// <param name="newImage">The desktop image changed since the last frame (Desktop Duplication).</param>
    /// <returns>The smart inversion weights used, or null.</returns>
    public ID3D11ShaderResourceView? Draw(
        ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, ID3D11RenderTargetView target, int width, int height,
        PhysicalRect monitor, in ModeFrame frame, DisplayEffects effects, WindowRegions windows, bool windowsFiltered,
        bool newImage = true)
    {
        // After a fall-back the smart entries show their matrix, "keep hue" (ADR-0024 decision 6).
        var smartActive = smart.Active;

        // Only when windows, filters or the fall-back changed: while nothing moves the upload is skipped.
        var effectsChanged = !ReferenceEquals(effects, _uploadedEffects);
        if (!ReferenceEquals(windows, _uploadedWindows) || effectsChanged || smartActive != _uploadedSmart)
        {
            WindowConstants.Fill(ref _windowConstants, windows, monitor, effects, smartActive);
            pass.UpdateWindows(context, _windowConstants);
            _uploadedWindows = windows;
            _uploadedEffects = effects;
            _uploadedSmart = smartActive;
        }

        var matrix = effects.Matrix;
        // The weights depend only on the desktop image and the photo protection (part of effects).
        var weights = smart.Prepare(
            context, desktop, effects.IsSmart || windows.UsesSmart(monitor), effects.ProtectPhotos, newImage || effectsChanged);
        var monitorSmart = effects.IsSmart && weights is not null;
        if (weights is not null)
        {
            ProbeSmartCost(context, desktop, target, width, height, monitor, frame.HasFrame ? monitor : frame.Source,
                effects, windows, monitorSmart, weights);
        }

        magnifyTime.Collect(context);
        magnifyTime.Begin(context);
        if (!frame.HasFrame)
        {
            pass.Draw(context, desktop, target, width, height,
                MagnifyConstants.Fullscreen(frame.Source, monitor, matrix, effects.Smoothing, monitorSmart), weights);
            magnifyTime.End(context);
            return weights;
        }

        // Lens/docked: outside the target the monitor looks as it does at 1× (colour filter and
        // filter per window go on), so nothing turns white when the lens comes on. With nothing to
        // filter it stays transparent. Click-through either way (window style).
        if (effects.Filter.ChangesColors || windowsFiltered)
        {
            pass.Draw(context, desktop, target, width, height,
                MagnifyConstants.Fullscreen(monitor, monitor, matrix, effects.Smoothing, monitorSmart), weights);
        }
        else
        {
            context.ClearRenderTargetView(target, s_transparent);
        }

        var viewport = Expand(frame.Target, FrameThickness, monitor);
        var constants = MagnifyConstants.WithFrame(frame.Source, monitor, frame.Target, viewport, FrameColor.Default, matrix, effects.Smoothing, monitorSmart);
        pass.Draw(context, desktop, target, (float)(viewport.X - monitor.X), (float)(viewport.Y - monitor.Y),
            (float)viewport.Width, (float)viewport.Height, constants, weights);
        magnifyTime.End(context);
        return weights;
    }

    // Once per pipeline (SmartCostProbe.Frames frames): the same desktop drawn plain and smart, each
    // timed, so the budget sees what the smart inversion adds to the magnify pass. The real draw
    // overwrites the whole target afterwards (fullscreen draw or clear), so nothing of it is seen.
    private void ProbeSmartCost(
        ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, ID3D11RenderTargetView target, int width, int height,
        PhysicalRect monitor, PhysicalRect source, DisplayEffects effects, WindowRegions windows, bool monitorSmart,
        ID3D11ShaderResourceView weights)
    {
        if (probe.Collect(context))
        {
            smart.SetMagnifyExtra(probe.ExtraMilliseconds);
        }

        // A few smart windows cost less than a smart monitor: measure again when that case starts.
        if (monitorSmart && !_probedMonitorSmart)
        {
            _probedMonitorSmart = true;
            probe.Restart();
        }

        probe.Tick();
        if (!probe.Wanted)
        {
            return;
        }

        // Plain: no palette entry is smart and the monitor matrix is applied without weights.
        WindowConstants.Fill(ref _probeConstants, windows, monitor, effects, smartActive: false);
        pass.UpdateWindows(context, _probeConstants);
        probe.BeginPlain(context);
        pass.Draw(context, desktop, target, width, height, MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, effects.Smoothing), null);
        probe.EndPlain(context);

        pass.UpdateWindows(context, _windowConstants);
        probe.BeginSmart(context);
        pass.Draw(context, desktop, target, width, height,
            MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, effects.Smoothing, monitorSmart), weights);
        probe.EndSmart(context);
    }

    private static PhysicalRect Expand(PhysicalRect rect, double by, PhysicalRect within)
    {
        var x = Math.Max(rect.X - by, within.X);
        var y = Math.Max(rect.Y - by, within.Y);
        return new PhysicalRect(x, y, Math.Min(rect.Right + by, within.Right) - x, Math.Min(rect.Bottom + by, within.Bottom) - y);
    }
}
