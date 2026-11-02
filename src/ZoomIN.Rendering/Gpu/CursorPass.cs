using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Draws pointer images and the locator ring with premultiplied alpha blending. Pointer textures
/// are created once per Windows cursor handle and cached, so drawing does not allocate.
/// </summary>
internal sealed class CursorPass : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pointerShader;
    private readonly ID3D11PixelShader _ringShader;
    private readonly ID3D11PixelShader _rectShader;
    private readonly ID3D11SamplerState _sampler;
    private readonly ID3D11BlendState _blend;
    private readonly ID3D11Buffer _constants;
    private readonly Dictionary<nint, PointerTexture?> _textures = [];

    // Standard pointers (Windows settings overridden) per handle and sharp size (1, 2, 4, 8).
    private readonly Dictionary<(nint Handle, int Sharp), PointerTexture?> _standard = [];

    public CursorPass(ID3D11Device device)
    {
        _device = device;
        _vertexShader = device.CreateVertexShader(ShaderSource.Compile("Cursor.hlsl", "VSMain", "vs_4_0").Span);
        _pointerShader = device.CreatePixelShader(ShaderSource.Compile("Cursor.hlsl", "PSCursor", "ps_4_0").Span);
        _ringShader = device.CreatePixelShader(ShaderSource.Compile("Cursor.hlsl", "PSRing", "ps_4_0").Span);
        _rectShader = device.CreatePixelShader(ShaderSource.Compile("Cursor.hlsl", "PSRect", "ps_4_0").Span);
        _sampler = device.CreateSamplerState(new SamplerDescription(
            Filter.MinMagMipLinear, TextureAddressMode.Border, TextureAddressMode.Border, TextureAddressMode.Border,
            0, 1, ComparisonFunction.Never, new Color4(0, 0, 0, 0)));
        // Premultiplied alpha: result = source + destination · (1 − source alpha).
        _blend = device.CreateBlendState(new BlendDescription(Blend.One, Blend.InverseSourceAlpha));
        _constants = device.CreateBuffer(new BufferDescription(CursorConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));
    }

    /// <summary>Texture for a pointer handle; loads it on first use. Null if the shape is unreadable.</summary>
    public PointerTexture? Texture(nint handle, ICursorShapeSource source)
    {
        if (!_textures.TryGetValue(handle, out var texture))
        {
            texture = source.Load(handle) is { } shape ? PointerTexture.Create(_device, shape) : null;
            _textures[handle] = texture;
        }

        return texture;
    }

    /// <summary>Standard Windows pointer for a handle, sharp up to pointer size <paramref name="sharp"/>; loaded on first use.</summary>
    public PointerTexture? StandardTexture(nint handle, int sharp, ICursorShapeSource source)
    {
        if (!_standard.TryGetValue((handle, sharp), out var texture))
        {
            texture = source.LoadStandard(handle, sharp) is { } shape ? PointerTexture.Create(_device, shape) : null;
            _standard[(handle, sharp)] = texture;
        }

        return texture;
    }

    public void DrawPointer(ID3D11DeviceContext context, ID3D11RenderTargetView target, PointerTexture pointer,
        float x, float y, float width, float height, in CursorConstants constants) =>
        Draw(context, target, _pointerShader, pointer.View, x, y, width, height, constants);

    public void DrawRing(ID3D11DeviceContext context, ID3D11RenderTargetView target, float centerX, float centerY,
        float radius, in CursorConstants constants) =>
        Draw(context, target, _ringShader, null, centerX - radius, centerY - radius, radius * 2, radius * 2, constants);

    public void DrawRect(ID3D11DeviceContext context, ID3D11RenderTargetView target, float x, float y,
        float width, float height, in CursorConstants constants) =>
        Draw(context, target, _rectShader, null, x, y, width, height, constants);

    public void Dispose()
    {
        foreach (var texture in _textures.Values.Concat(_standard.Values))
        {
            texture?.Dispose();
        }

        _constants.Dispose();
        _blend.Dispose();
        _sampler.Dispose();
        _rectShader.Dispose();
        _ringShader.Dispose();
        _pointerShader.Dispose();
        _vertexShader.Dispose();
    }

    private void Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, ID3D11PixelShader shader,
        ID3D11ShaderResourceView? texture, float x, float y, float width, float height, in CursorConstants constants)
    {
        context.UpdateSubresource(constants, _constants);
        context.OMSetRenderTargets(target);
        context.OMSetBlendState(_blend);
        context.RSSetViewport(x, y, width, height);
        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(shader);
        context.PSSetShaderResource(0, texture!);
        context.PSSetSampler(0, _sampler);
        context.PSSetConstantBuffer(0, _constants);
        context.Draw(3, 0);

        // The magnify pass of the next frame expects blending off.
        context.OMSetBlendState(null);
    }
}
