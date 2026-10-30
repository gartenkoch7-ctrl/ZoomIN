using System.Diagnostics;
using Vortice.Direct3D11;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Magnified pointer and locator ring for one monitor (M5). Decides where the pointer appears,
/// hides the system pointer while the magnified one is drawn, and animates the locator.
/// Runs every frame: no allocations after a pointer shape was seen once.
/// </summary>
internal sealed class CursorLayer
{
    private const double MinRimPixels = 2;
    private const double LocatorSeconds = 1.0;
    private const double LocatorStartRadius = 150;
    private const double LocatorEndRadius = 40;
    private const double IdleSecondsBeforeAutoLocate = 3;

    private readonly CursorPass _pass;
    private readonly ICursorShapeSource _shapes;
    private readonly ISystemCursor _systemCursor;
    private PhysicalPoint _lastPosition;
    private long _lastMove;
    private long _autoLocateAt;

    public CursorLayer(CursorPass pass, ICursorShapeSource shapes, ISystemCursor systemCursor)
    {
        _pass = pass;
        _shapes = shapes;
        _systemCursor = systemCursor;
    }

    /// <summary>Draws pointer and locator; returns true while the locator is still animating.</summary>
    public bool Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, in ModeFrame frame,
        ZoomMode mode, double zoom, PhysicalPoint cursor, DisplayEffects effects, long locateRequestedAt)
    {
        var now = Stopwatch.GetTimestamp();
        TrackMovement(cursor, now, effects.Cursor.AutoLocate);

        // The pointer appears where its desktop position is shown. Around the mouse (fixpoint)
        // that is the real position; while the view follows focus/caret it is mapped (M6).
        var docked = mode == ZoomMode.Docked && frame.HasFrame;
        var magnified = zoom > 1.01;
        var shown = FrameMapping.Map(frame, cursor);
        if (magnified && frame.Source.Contains(cursor))
        {
            DrawPointer(context, target, monitor, shown, zoom, effects.Cursor);
        }

        // Magnified: the real pointer would sit somewhere else in the image, so it is hidden.
        // Docked: the real pointer works in the rest of the screen and stays visible.
        _systemCursor.SetVisible(!magnified || docked);

        return DrawLocator(context, target, monitor, shown, now, Math.Max(locateRequestedAt, _autoLocateAt));
    }

    /// <summary>Called when this monitor stops zooming while the pointer is on it.</summary>
    public void ShowSystemCursor() => _systemCursor.SetVisible(true);

    private void DrawPointer(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        PhysicalPoint shown, double zoom, CursorStyle style)
    {
        if (!_shapes.TryGetCurrent(out var handle) || _pass.Texture(handle, _shapes) is not { } pointer)
        {
            return;
        }

        var scale = style.ScaleFor(zoom);
        var rim = style.Rim ? Math.Max(MinRimPixels, scale) : 0;
        var width = (pointer.Width * scale) + (2 * rim);
        var height = (pointer.Height * scale) + (2 * rim);
        var constants = CursorConstants.ForPointer(
            (float)(rim / width), (float)(rim / height), pointer.Width, pointer.Height, (float)(rim / scale), FrameColor.Default);

        _pass.DrawPointer(
            context,
            target,
            pointer,
            (float)(shown.X - (pointer.HotspotX * scale) - rim - monitor.X),
            (float)(shown.Y - (pointer.HotspotY * scale) - rim - monitor.Y),
            (float)width,
            (float)height,
            constants);
    }

    private bool DrawLocator(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor,
        PhysicalPoint shown, long now, long startedAt)
    {
        var elapsed = startedAt == 0 ? double.MaxValue : Stopwatch.GetElapsedTime(startedAt, now).TotalSeconds;
        if (elapsed >= LocatorSeconds)
        {
            return false;
        }

        // Ring shrinks onto the pointer and fades slightly.
        var t = elapsed / LocatorSeconds;
        var radius = LocatorStartRadius + ((LocatorEndRadius - LocatorStartRadius) * t);
        var alpha = (float)(1 - (0.5 * t));
        var color = FrameColor.Default;
        var ring = new FrameColor(color.R * alpha, color.G * alpha, color.B * alpha, alpha);

        _pass.DrawRing(context, target, (float)(shown.X - monitor.X), (float)(shown.Y - monitor.Y), (float)radius,
            CursorConstants.ForRing(0.12f, ring));
        return true;
    }

    private void TrackMovement(PhysicalPoint cursor, long now, bool autoLocate)
    {
        if (cursor == _lastPosition)
        {
            return;
        }

        if (autoLocate && _lastMove != 0 && Stopwatch.GetElapsedTime(_lastMove, now).TotalSeconds >= IdleSecondsBeforeAutoLocate)
        {
            _autoLocateAt = now;
        }

        _lastPosition = cursor;
        _lastMove = now;
    }
}
