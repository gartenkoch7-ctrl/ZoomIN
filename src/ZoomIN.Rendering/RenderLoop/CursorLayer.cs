using System.Diagnostics;
using Vortice.Direct3D11;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Pointer, crosshair and locator ring for one monitor (M5, pointer page). Decides where the
/// pointer appears, hides the system pointer while ZoomIN draws its own, and animates the
/// locator. Runs every frame: no allocations after a pointer shape was seen once.
/// </summary>
internal sealed class CursorLayer
{
    private const double LocatorEndRadius = 40;

    private readonly CursorPass _pass;
    private readonly PointerBackdrop _backdrop;
    private readonly ICursorShapeSource _shapes;
    private readonly ISystemCursor _systemCursor;
    private PointerPlacement _placement;
    private PointerPlacement _realPlacement;
    private bool _systemVisible = true;

    public CursorLayer(CursorPass pass, PointerBackdrop backdrop, ICursorShapeSource shapes, ISystemCursor systemCursor)
    {
        _pass = pass;
        _backdrop = backdrop;
        _shapes = shapes;
        _systemCursor = systemCursor;
    }

    /// <summary>Draws crosshair, pointer and locator; returns true while the locator is still animating.</summary>
    /// <param name="hidden">The user is typing and the pointer should disappear.</param>
    public bool Draw(ID3D11DeviceContext context, in LayerTarget target, PhysicalRect monitor, in ModeFrame frame,
        ZoomMode mode, double zoom, PhysicalPoint cursor, DisplayEffects effects, long locateRequestedAt, bool hidden)
    {
        try
        {
            return DrawLayer(context, target, monitor, frame, mode, zoom, cursor, effects.Cursor, locateRequestedAt, hidden);
        }
        finally
        {
            _pass.End(context);
        }
    }

    /// <summary>Idle frame (M14 step 3): repeats the last wanted system pointer visibility.</summary>
    public void KeepSystemCursor() => _systemCursor.SetVisible(_systemVisible);

    /// <summary>Called when this monitor stops drawing while the pointer is on it.</summary>
    public void ShowSystemCursor() => _systemCursor.SetVisible(true);

    private bool DrawLayer(ID3D11DeviceContext context, in LayerTarget target, PhysicalRect monitor, in ModeFrame frame,
        ZoomMode mode, double zoom, PhysicalPoint cursor, PointerStyle style, long locateRequestedAt, bool hidden)
    {
        // The pointer appears where its desktop position is shown. Around the mouse (fixpoint)
        // that is the real position; while the view follows focus/caret it is mapped (M6).
        var docked = mode == ZoomMode.Docked && frame.HasFrame;
        var drawn = style.DrawsPointer(zoom);
        var shown = FrameMapping.Map(frame, cursor);
        var inView = frame.Source.Contains(cursor);
        if (!hidden && inView && style.CrosshairVisible(zoom))
        {
            DrawCrosshair(context, target.View, monitor, frame, shown, style);
        }

        if (!hidden && drawn && inView)
        {
            DrawPointer(context, target, monitor, shown, zoom, style, ref _placement);
        }

        // Docked: the real pointer works in the rest of the screen and over the strip. The adaptive
        // pointer (M15) replaces it at the real position, so it looks the same everywhere.
        var everywhere = style.NeedsDesktop;
        if (!hidden && drawn && docked && everywhere)
        {
            DrawPointer(context, target, monitor, cursor, zoom, style, ref _realPlacement);
        }

        // ZoomIN draws the pointer: the real one would sit somewhere else in the image, so it is hidden.
        _systemVisible = !hidden && (!drawn || (docked && !everywhere));
        _systemCursor.SetVisible(_systemVisible);

        return DrawLocator(context, target.View, monitor, shown, Stopwatch.GetTimestamp(), locateRequestedAt, style);
    }

    private void DrawPointer(ID3D11DeviceContext context, in LayerTarget target, PhysicalRect monitor,
        PhysicalPoint shown, double zoom, PointerStyle style, ref PointerPlacement placement)
    {
        var scale = style.ScaleFor(zoom);
        if (!_shapes.TryGetCurrent(out var handle))
        {
            return;
        }

        var pointer = style.OverrideWindows
            ? _pass.StandardTexture(handle, PointerStyle.SharpScale(scale), _shapes)
            : _pass.Texture(handle, _shapes);
        if (pointer is null)
        {
            return;
        }

        // Screen pixels per image pixel: sharp standard images carry more pixels than they cover.
        var texel = scale / pointer.Density;
        var rim = style.RimPixelsFor(scale);
        var width = (pointer.Width * texel) + (2 * rim);
        var height = (pointer.Height * texel) + (2 * rim);
        var adaptive = style.NeedsDesktop && target.Desktop is not null;

        // Without a captured desktop the adaptive pointer cannot see its background: drawn as is.
        var mode = style.ColorMode == PointerColorMode.Adaptive && !adaptive ? PointerColorMode.Original : style.ColorMode;
        var tint = FrameColor.From(style.Color) with { A = (float)mode };

        // Near the bottom/right edge the image is mirrored so it stays on screen; the tip keeps
        // marking the real click point.
        placement = PointerPlacement.Place(
            shown, width, height, (pointer.HotspotX * texel) + rim, (pointer.HotspotY * texel) + rim, monitor, placement);
        var constants = CursorConstants.ForPointer(
            (float)(rim / width), (float)(rim / height), pointer.Width, pointer.Height, (float)(rim / texel),
            FrameColor.From(style.RimColor), tint, placement.FlipX, placement.FlipY);
        var x = placement.X - monitor.X;
        var y = placement.Y - monitor.Y;

        if (!adaptive)
        {
            _pass.DrawPointer(context, target.View, pointer, (float)x, (float)y, (float)width, (float)height, constants);
            return;
        }

        // What is drawn under the pointer so far (zoom, filters, crosshair) is copied first: the
        // shader cannot read the target it draws into (ADR-0026).
        if (!_backdrop.Capture(context, target.Texture, target.Width, target.Height, x, y, width, height)
            || _backdrop.View is not { } copy)
        {
            return; // nothing of the pointer lies on the target
        }

        var background = new AdaptiveBackground(
            FrameColor.From(style.AdaptiveOnDark), FrameColor.From(style.AdaptiveOnLight), (float)style.AdaptiveThreshold,
            _backdrop.X, _backdrop.Y, _backdrop.InverseWidth, _backdrop.InverseHeight, 1f / target.Width, 1f / target.Height);
        _pass.DrawAdaptivePointer(context, target.View, pointer, copy, target.Desktop!,
            (float)x, (float)y, (float)width, (float)height, constants.WithAdaptive(background));
    }

    // Two lines through the pointer across the visible view (lens/strip or the whole monitor).
    private void DrawCrosshair(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        in ModeFrame frame, PhysicalPoint shown, PointerStyle style)
    {
        var area = frame.HasFrame ? frame.Target : monitor;
        var width = style.CrosshairWidth;
        var fill = CursorConstants.ForRect(0.5f, 0.5f, FrameColor.From(style.CrosshairColor, style.CrosshairOpacity));
        _pass.DrawRect(context, target, (float)(area.X - monitor.X), (float)(shown.Y - (width / 2) - monitor.Y),
            (float)area.Width, (float)width, fill);
        _pass.DrawRect(context, target, (float)(shown.X - (width / 2) - monitor.X), (float)(area.Y - monitor.Y),
            (float)width, (float)area.Height, fill);
    }

    private bool DrawLocator(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        PhysicalPoint shown, long now, long startedAt, PointerStyle style)
    {
        var elapsed = startedAt == 0 ? double.MaxValue : Stopwatch.GetElapsedTime(startedAt, now).TotalSeconds;
        if (elapsed >= style.LocatorSeconds)
        {
            return false;
        }

        // Ring shrinks onto the pointer and fades slightly.
        var t = elapsed / style.LocatorSeconds;
        var radius = style.LocatorSize + ((LocatorEndRadius - style.LocatorSize) * t);
        var ring = FrameColor.From(style.LocatorColor, 1 - (0.5 * t));

        _pass.DrawRing(context, target, (float)(shown.X - monitor.X), (float)(shown.Y - monitor.Y), (float)radius,
            CursorConstants.ForRing(0.12f, ring));
        return true;
    }
}
