using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// D1 spike (never merged): a 400 × 240 test desktop with real 9-pt ClearType text drawn by GDI.
/// Quadrants: dark text on paper, light text on a dark editor, a spreadsheet grid next to a dark
/// row-number bar, a synthetic photo. BGRA bytes, top-down.
/// </summary>
internal static partial class D1SpikeScene
{
    public const int Width = 400;
    public const int Height = 240;

    // Centres of the quadrants, for zooming onto one of them.
    public static readonly (string Name, int X, int Y)[] Spots =
    [
        ("paper", 60, 30), ("dark", 260, 30), ("grid", 60, 150), ("photo", 300, 180),
    ];

    public static byte[] Create()
    {
        var hdc = CreateCompatibleDC(0);
        var info = new BitmapInfoHeader { Size = 40, Width = Width, Height = -Height, Planes = 1, BitCount = 32 };
        var bitmap = CreateDIBSection(hdc, ref info, 0, out var bits, 0, 0);
        var old = SelectObject(hdc, bitmap);
        var pixels = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var (r, g, b) = Background(x, y);
                var at = ((y * Width) + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (b, g, r, 255);
            }
        }

        Marshal.Copy(pixels, 0, bits, pixels.Length);

        // 9 pt at 96 dpi = 12 px; quality 5 = CLEARTYPE_QUALITY, so the colour fringes are real.
        var font = CreateFontW(-12, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        var oldFont = SelectObject(hdc, font);
        _ = SetBkMode(hdc, 1);
        Text(hdc, 0x000000, 4, 4, "Lesbarkeit bei 9 pt: Hamburgefonstiv", "Ein dünner Strich, ilI1 rn m 0O", "Kleine Schrift im Dokument.");
        Text(hdc, 0xDCDCDC, 204, 4, "Editor dunkel: var x = 42;", "Kommentar // il1 rn m", "Hellgraue Schrift auf Grau.");
        for (var row = 0; row < 6; row++)
        {
            TextOutW(hdc, 4, 124 + (row * 19), (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
        }

        _ = SetTextColor(hdc, 0x000000);
        for (var row = 0; row < 6; row++)
        {
            var cell = (1234.5 * (row + 1)).ToString("N1", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            TextOutW(hdc, 24, 124 + (row * 19), cell, cell.Length);
            TextOutW(hdc, 104, 124 + (row * 19), "Summe", 5);
        }

        GdiFlush();
        Marshal.Copy(bits, pixels, 0, pixels.Length);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255; // GDI leaves alpha at 0 where it drew
        }

        SelectObject(hdc, oldFont);
        DeleteObject(font);
        SelectObject(hdc, old);
        DeleteObject(bitmap);
        DeleteDC(hdc);
        return pixels;
    }

    private static void Text(nint hdc, uint colour, int x, int y, params string[] lines)
    {
        _ = SetTextColor(hdc, colour);
        for (var i = 0; i < lines.Length; i++)
        {
            TextOutW(hdc, x, y + (i * 16), lines[i], lines[i].Length);
        }
    }

    private static (byte R, byte G, byte B) Background(int x, int y)
    {
        if (y < 120)
        {
            return x < 200 ? ((byte)255, (byte)255, (byte)255) : ((byte)30, (byte)30, (byte)30);
        }

        if (x < 200)
        {
            if (x < 18)
            {
                return (40, 40, 40); // dark row-number bar (row numbers drawn light below)
            }

            return x % 80 == 18 || (y - 120) % 19 == 0 ? ((byte)212, (byte)212, (byte)212) : ((byte)255, (byte)255, (byte)255);
        }

        // Synthetic photo: sky gradient, a soft sun, a darker hill with fine texture.
        var u = (x - 200) / 200.0;
        var v = (y - 120) / 120.0;
        var sun = Math.Exp(-(((u - 0.7) * (u - 0.7)) + ((v - 0.3) * (v - 0.3))) * 30);
        var hill = v > 0.6 + (0.15 * Math.Sin(u * 6)) ? 1.0 : 0.0;
        var grain = ((x * 37) + (y * 91)) % 17 / 17.0 * 0.08;
        var r = (0.4 + (0.5 * sun)) * (1 - hill) + (hill * (0.2 + grain));
        var g = (0.6 + (0.3 * sun)) * (1 - hill) + (hill * (0.45 + grain));
        var b = (0.9 - (0.3 * sun)) * (1 - hill) + (hill * (0.15 + grain));
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
        public int Palette;
    }

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateDIBSection(nint hdc, ref BitmapInfoHeader info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateFontW(
        int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut,
        uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

    [LibraryImport("gdi32.dll")]
    private static partial uint SetTextColor(nint hdc, uint colour);

    [LibraryImport("gdi32.dll")]
    private static partial int SetBkMode(nint hdc, int mode);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TextOutW(nint hdc, int x, int y, string text, int length);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GdiFlush();
}
