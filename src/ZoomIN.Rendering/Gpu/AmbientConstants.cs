using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Ambient.hlsl. 32 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct AmbientConstants(float textureWidth, float textureHeight, float alpha, bool protectPhotos, float block)
{
    public const int SizeInBytes = 32;

    public readonly float TextureWidth = textureWidth;
    public readonly float TextureHeight = textureHeight;
    public readonly float Alpha = alpha;
    public readonly uint ProtectPhotos = protectPhotos ? 1u : 0u;
    public readonly float Block = block;
    public readonly float Padding0;
    public readonly float Padding1;
    public readonly float Padding2;
}
