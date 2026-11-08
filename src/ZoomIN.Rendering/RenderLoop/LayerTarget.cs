using Vortice.Direct3D11;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// The overlay image a layer draws into. The texture and size let the adaptive pointer (M15) copy
/// what is already drawn; <paramref name="Desktop"/> is the captured desktop under it, null when
/// nothing is captured this frame.
/// </summary>
internal readonly record struct LayerTarget(
    ID3D11RenderTargetView View, ID3D11Texture2D Texture, int Width, int Height, ID3D11ShaderResourceView? Desktop);
