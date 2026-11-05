using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// Shrinks a desktop area into a small 32-bit bitmap for one brightness look (ADR-0020). Uses GDI
/// on the tracker thread instead of the GPU, so a look never wakes a sleeping render thread.
/// ZoomIN's overlays are excluded from capture (WDA_EXCLUDEFROMCAPTURE), so the look sees the
/// original desktop, not ZoomIN's filtered picture.
/// </summary>
internal sealed unsafe class ScreenSampler : IDisposable
{
    /// <summary>Width and height of the shrunk picture; 4096 pixels are plenty for a share.</summary>
    public const int Size = 64;

    private readonly HDC _memory;
    private readonly HBITMAP _bitmap;
    private readonly HGDIOBJ _previous;
    private readonly uint* _bits;

    public ScreenSampler()
    {
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER),
            biWidth = Size,
            biHeight = -Size, // top-down rows
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0, // BI_RGB
        };
        var info = new BITMAPINFO { bmiHeader = header };

        _memory = PInvoke.CreateCompatibleDC(HDC.Null);
        void* bits;
        _bitmap = PInvoke.CreateDIBSection(_memory, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
        if (_memory == HDC.Null || _bitmap == HBITMAP.Null || bits is null)
        {
            Dispose();
            throw new InvalidOperationException("No memory bitmap for brightness looks.");
        }

        _bits = (uint*)bits;
        _previous = PInvoke.SelectObject(_memory, _bitmap);

        // HALFTONE averages the pixels it drops, so text on a white page still counts as light.
        _ = PInvoke.SetStretchBltMode(_memory, STRETCH_BLT_MODE.HALFTONE);
        PInvoke.SetBrushOrgEx(_memory, 0, 0, null);
    }

    /// <summary>The shrunk picture of the last <see cref="Capture"/>, 0xAARRGGBB, rows top-down.</summary>
    public ReadOnlySpan<uint> Pixels => new(_bits, Size * Size);

    /// <summary>Copies <paramref name="area"/> of the desktop; false if GDI refused (e.g. secure desktop shown).</summary>
    public bool Capture(PhysicalRect area)
    {
        var screen = PInvoke.GetDC(HWND.Null);
        if (screen == HDC.Null)
        {
            return false;
        }

        try
        {
            return PInvoke.StretchBlt(
                _memory, 0, 0, Size, Size, screen,
                (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, ROP_CODE.SRCCOPY);
        }
        finally
        {
            _ = PInvoke.ReleaseDC(HWND.Null, screen);
        }
    }

    public void Dispose()
    {
        if (_previous != HGDIOBJ.Null)
        {
            PInvoke.SelectObject(_memory, _previous);
        }

        if (_bitmap != HBITMAP.Null)
        {
            PInvoke.DeleteObject(_bitmap);
        }

        if (_memory != HDC.Null)
        {
            PInvoke.DeleteDC(_memory);
        }
    }
}
