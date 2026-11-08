using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Draws pointer images and the locator ring with premultiplied alpha blending. Pointer textures
/// are created once per Windows cursor handle and cached, so drawing does not allocate.
/// The shared pipeline state is set by the first draw of a layer; the layer ends with
/// <see cref="End"/>, before any other pass draws (M14 step 2).
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

    // Last lookups: the pointer shape rarely changes, so most frames skip the dictionary.
    private nint _lastHandle;
    private PointerTexture? _lastTexture;
    private (nint Handle, int Sharp) _lastStandardKey;
    private PointerTexture? _lastStandard;

    private ConstantUpload<CursorConstants> _uploaded;
    private ID3D11RenderTargetView? _bound;

    public CursorPass(ID3D11Device device)
    {
        _device = device;
        _vertexShader = device.CreateVertexShader(ShaderSource.Load("Cursor.hlsl", "VSMain", "vs_4_0"));
        _pointerShader = device.CreatePixelShader(ShaderSource.Load("Cursor.hlsl", "PSCursor", "ps_4_0"));
        _ringShader = device.CreatePixelShader(ShaderSource.Load("Cursor.hlsl", "PSRing", "ps_4_0"));
        _rectShader = device.CreatePixelShader(ShaderSource.Load("Cursor.hlsl", "PSRect", "ps_4_0"));
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
        if (handle == _lastHandle && handle != 0)
        {
            return _lastTexture;
        }

        if (!_textures.TryGetValue(handle, out var texture))
        {
            texture = source.Load(handle) is { } shape ? PointerTexture.Create(_device, shape) : null;
            _textures[handle] = texture;
        }

        (_lastHandle, _lastTexture) = (handle, texture);
        return texture;
    }

    /// <summary>Standard Windows pointer for a handle, sharp up to pointer size <paramref name="sharp"/>; loaded on first use.</summary>
    public PointerTexture? StandardTexture(nint handle, int sharp, ICursorShapeSource source)
    {
        if (_lastStandardKey == (handle, sharp) && handle != 0)
        {
            return _lastStandard;
        }

        if (!_standard.TryGetValue((handle, sharp), out var texture))
        {
            texture = source.LoadStandard(handle, sharp) is { } shape ? PointerTexture.Create(_device, shape) : null;
            _standard[(handle, sharp)] = texture;
        }

        (_lastStandardKey, _lastStandard) = ((handle, sharp), texture);
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

    /// <summary>Ends a layer: blending off again, which the magnify pass of the next frame expects.</summary>
    public void End(ID3D11DeviceContext context)
    {
        if (_bound is null)
        {
            return;
        }

        context.OMSetBlendState(null);
        _bound = null;
    }

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
        if (!ReferenceEquals(_bound, target))
        {
            Bind(context, target);
        }

        _uploaded.Update(context, _constants, constants);
        context.RSSetViewport(x, y, width, height);
        context.PSSetShader(shader);
        context.PSSetShaderResource(0, texture!);
        context.Draw(3, 0);
    }

    // State that stays the same for every draw of a layer.
    private void Bind(ID3D11DeviceContext context, ID3D11RenderTargetView target)
    {
        context.OMSetRenderTargets(target);
        context.OMSetBlendState(_blend);
        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetSampler(0, _sampler);
        context.PSSetConstantBuffer(0, _constants);
        _bound = target;
    }
}
