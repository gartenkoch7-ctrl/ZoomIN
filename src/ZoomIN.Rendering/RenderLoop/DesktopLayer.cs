using Vortice.Direct3D11;
using Vortice.Mathematics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Draws the captured desktop of one monitor with zoom, colour filter, filter per window, smart
/// inversion and "Schärfe"/"Fett" into the overlay. One pass: the enhancement only swaps the pixel
/// shader (spike D1, variant A). <see cref="Draw"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class DesktopLayer(
    MagnifyPass pass, SmartInvertLayer smart, GpuPassTimer magnifyTime, PassCostProbe probe, EnhanceCost enhanceCost)
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

        // Only the magnified view is enhanced; the rest of the monitor around a lens shows 1×.
        var enhance = effects.Enhancement.At(frame, effects.Smoothing);
        var viewport = frame.HasFrame ? Expand(frame.Target, FrameThickness, monitor) : new PhysicalRect(monitor.X, monitor.Y, width, height);
        if (enhanceCost.Tick(context, enhance))
        {
            ProbeEnhanceCost(context, desktop, target, monitor, viewport, frame, effects, monitorSmart, weights, enhance);
        }

        magnifyTime.Collect(context);
        magnifyTime.Begin(context);
        if (!frame.HasFrame)
        {
            pass.Draw(context, desktop, target, width, height, Magnified(frame, monitor, viewport, effects, monitorSmart, enhance), weights);
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

        DrawMagnified(context, desktop, target, monitor, viewport, Magnified(frame, monitor, viewport, effects, monitorSmart, enhance), weights);
        magnifyTime.End(context);
        return weights;
    }

    // Once per pipeline (PassCostProbe.Frames frames): the same desktop drawn plain and smart, each
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
        probe.BeginBase(context);
        pass.Draw(context, desktop, target, width, height, MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, effects.Smoothing), null);
        probe.EndBase(context);

        pass.UpdateWindows(context, _windowConstants);
        probe.BeginVariant(context);
        pass.Draw(context, desktop, target, width, height,
            MagnifyConstants.Fullscreen(source, monitor, effects.Matrix, effects.Smoothing, monitorSmart), weights);
        probe.EndVariant(context);
    }

    // After a change of "Schärfe"/"Fett", while someone listens (EnhanceCost): the magnified view
    // drawn exactly as below, once without and once with them, each timed. Inside the view the real
    // draw overwrites it; outside a lens it is cleared or drawn over afterwards too.
    private void ProbeEnhanceCost(
        ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, ID3D11RenderTargetView target, PhysicalRect monitor,
        PhysicalRect viewport, in ModeFrame frame, DisplayEffects effects, bool monitorSmart, ID3D11ShaderResourceView? weights,
        TextEnhancement enhance)
    {
        var cost = enhanceCost.Probe;
        cost.BeginBase(context);
        DrawMagnified(context, desktop, target, monitor, viewport, Magnified(frame, monitor, viewport, effects, monitorSmart, TextEnhancement.Off), weights);
        cost.EndBase(context);

        cost.BeginVariant(context);
        DrawMagnified(context, desktop, target, monitor, viewport, Magnified(frame, monitor, viewport, effects, monitorSmart, enhance), weights);
        cost.EndVariant(context);
    }

    // The magnified view: the whole monitor in fullscreen, the target with its frame in lens/docked.
    private static MagnifyConstants Magnified(
        in ModeFrame frame, PhysicalRect monitor, PhysicalRect viewport, DisplayEffects effects, bool monitorSmart, TextEnhancement enhance) =>
        frame.HasFrame
            ? MagnifyConstants.WithFrame(
                frame.Source, monitor, frame.Target, viewport, FrameColor.Default, effects.Matrix, effects.Smoothing, monitorSmart, enhance)
            : MagnifyConstants.Fullscreen(frame.Source, monitor, effects.Matrix, effects.Smoothing, monitorSmart, enhance);

    private void DrawMagnified(
        ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, ID3D11RenderTargetView target, PhysicalRect monitor,
        PhysicalRect viewport, in MagnifyConstants constants, ID3D11ShaderResourceView? weights) =>
        pass.Draw(context, desktop, target, (float)(viewport.X - monitor.X), (float)(viewport.Y - monitor.Y),
            (float)viewport.Width, (float)viewport.Height, constants, weights);

    /// <summary>The frame around <paramref name="rect"/>, clipped to <paramref name="within"/> (also for islands, M19 B1).</summary>
    internal static PhysicalRect Expand(PhysicalRect rect, double by, PhysicalRect within)
    {
        var x = Math.Max(rect.X - by, within.X);
        var y = Math.Max(rect.Y - by, within.Y);
        return new PhysicalRect(x, y, Math.Min(rect.Right + by, within.Right) - x, Math.Min(rect.Bottom + by, within.Bottom) - y);
    }
}
