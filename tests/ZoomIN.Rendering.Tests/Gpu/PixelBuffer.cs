using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>One BGRA8 pixel, laid out as in a B8G8R8A8_UNorm texture.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Bgra(byte B, byte G, byte R, byte A);

/// <summary>Read-back pixels of a square render target.</summary>
internal sealed class PixelBuffer(Bgra[] pixels, int size)
{
    public Bgra At(int x, int y) => pixels[(y * size) + x];
}
