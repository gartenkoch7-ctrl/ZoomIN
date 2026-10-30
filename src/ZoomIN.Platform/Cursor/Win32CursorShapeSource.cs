using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Cursor;

/// <summary>Reads the current pointer via GetCursorInfo and its image via GetIconInfo/GetDIBits.</summary>
public sealed unsafe class Win32CursorShapeSource : ICursorShapeSource
{
    private const uint CursorShowing = 0x1;

    public bool TryGetCurrent(out nint handle)
    {
        var info = new CURSORINFO { cbSize = (uint)sizeof(CURSORINFO) };
        var ok = PInvoke.GetCursorInfo(ref info) && ((uint)info.flags & CursorShowing) != 0 && !info.hCursor.IsNull;
        handle = ok ? (nint)info.hCursor.Value : 0;
        return ok;
    }

    public CursorShape? Load(nint handle)
    {
        ICONINFO icon;
        if (!PInvoke.GetIconInfo(new HICON((void*)handle), &icon))
        {
            return null;
        }

        try
        {
            var mask = ReadBitmap(icon.hbmMask, out var maskWidth, out var maskHeight);
            if (mask is null)
            {
                return null;
            }

            // Monochrome pointers have no colour bitmap; their mask holds AND and XOR halves.
            if (icon.hbmColor.IsNull)
            {
                var height = maskHeight / 2;
                return new CursorShape(handle, maskWidth, height, (int)icon.xHotspot, (int)icon.yHotspot,
                    CursorPixels.FromMonochrome(mask, maskWidth, height));
            }

            var color = ReadBitmap(icon.hbmColor, out var width, out var colorHeight);
            return color is null
                ? null
                : new CursorShape(handle, width, colorHeight, (int)icon.xHotspot, (int)icon.yHotspot,
                    CursorPixels.FromColor(color, mask, width, colorHeight));
        }
        finally
        {
            PInvoke.DeleteObject(icon.hbmMask);
            if (!icon.hbmColor.IsNull)
            {
                PInvoke.DeleteObject(icon.hbmColor);
            }
        }
    }

    /// <summary>Any bitmap as 32-bit BGRA, top-down.</summary>
    private static byte[]? ReadBitmap(HBITMAP bitmap, out int width, out int height)
    {
        BITMAP info;
        var read = PInvoke.GetObject(bitmap, sizeof(BITMAP), &info);
        width = info.bmWidth;
        height = info.bmHeight;
        if (read == 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        var header = new BITMAPINFO();
        header.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        header.bmiHeader.biWidth = width;
        header.bmiHeader.biHeight = -height; // negative = top-down rows
        header.bmiHeader.biPlanes = 1;
        header.bmiHeader.biBitCount = 32;

        var pixels = new byte[width * height * 4];
        var dc = PInvoke.GetDC(HWND.Null);
        try
        {
            fixed (byte* p = pixels)
            {
                return PInvoke.GetDIBits(dc, bitmap, 0, (uint)height, p, &header, DIB_USAGE.DIB_RGB_COLORS) == height ? pixels : null;
            }
        }
        finally
        {
            _ = PInvoke.ReleaseDC(HWND.Null, dc);
        }
    }
}
