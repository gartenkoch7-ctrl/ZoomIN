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
    private readonly ICursorShapeSource _shapes;
    private readonly ISystemCursor _systemCursor;

    public CursorLayer(CursorPass pass, ICursorShapeSource shapes, ISystemCursor systemCursor)
    {
        _pass = pass;
        _shapes = shapes;
        _systemCursor = systemCursor;
    }

    /// <summary>Draws crosshair, pointer and locator; returns true while the locator is still animating.</summary>
    /// <param name="hidden">The user is typing and the pointer should disappear.</param>
    public bool Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, in ModeFrame frame,
        ZoomMode mode, double zoom, PhysicalPoint cursor, DisplayEffects effects, long locateRequestedAt, bool hidden)
    {
        var style = effects.Cursor;

        // The pointer appears where its desktop position is shown. Around the mouse (fixpoint)
        // that is the real position; while the view follows focus/caret it is mapped (M6).
        var docked = mode == ZoomMode.Docked && frame.HasFrame;
        var drawn = style.DrawsPointer(zoom);
        var shown = FrameMapping.Map(frame, cursor);
        var inView = frame.Source.Contains(cursor);
        if (!hidden && inView && style.CrosshairVisible(zoom))
        {
            DrawCrosshair(context, target, monitor, frame, shown, style);
        }

        if (!hidden && drawn && inView)
        {
            DrawPointer(context, target, monitor, shown, zoom, style);
        }

        // ZoomIN draws the pointer: the real one would sit somewhere else in the image, so it is
        // hidden. Docked: the real pointer works in the rest of the screen and stays visible.
        _systemCursor.SetVisible(!hidden && (!drawn || docked));

        return DrawLocator(context, target, monitor, shown, Stopwatch.GetTimestamp(), locateRequestedAt, style);
    }

    /// <summary>Called when this monitor stops drawing while the pointer is on it.</summary>
    public void ShowSystemCursor() => _systemCursor.SetVisible(true);

    private void DrawPointer(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        PhysicalPoint shown, double zoom, PointerStyle style)
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
        var tint = FrameColor.From(style.Color) with { A = (float)style.ColorMode };
        var constants = CursorConstants.ForPointer(
            (float)(rim / width), (float)(rim / height), pointer.Width, pointer.Height, (float)(rim / texel),
            FrameColor.From(style.RimColor), tint);

        _pass.DrawPointer(
            context,
            target,
            pointer,
            (float)(shown.X - (pointer.HotspotX * texel) - rim - monitor.X),
            (float)(shown.Y - (pointer.HotspotY * texel) - rim - monitor.Y),
            (float)width,
            (float)height,
            constants);
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
