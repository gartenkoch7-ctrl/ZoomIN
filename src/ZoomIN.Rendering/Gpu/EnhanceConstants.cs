using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// D1 spike (never merged): constant buffer of Enhance.hlsli, register b2. 32 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct EnhanceConstants
{
    public const int SizeInBytes = 32;

    public readonly float Sharpness;
    public readonly float BoldPx;
    public readonly float SharpenReach;
    private readonly float _padding;
    public readonly float OutputTexelU;
    public readonly float OutputTexelV;
    private readonly float _padding2;
    private readonly float _padding3;

    /// <param name="sharpness">0–1.</param>
    /// <param name="boldPx">0–3 output pixels.</param>
    /// <param name="zoom">Output pixels per source texel.</param>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    public EnhanceConstants(float sharpness, float boldPx, double zoom, int width, int height)
    {
        Sharpness = sharpness;
        BoldPx = boldPx;
        SharpenReach = (float)zoom;
        OutputTexelU = 1f / width;
        OutputTexelV = 1f / height;
    }
}
