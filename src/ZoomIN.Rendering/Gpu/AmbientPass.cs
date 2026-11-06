using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Smart inversion (ADR-0024): a small texture with one inversion weight per
/// <see cref="Block"/> × <see cref="Block"/> desktop pixels, kept over frames for the time
/// smoothing (two textures, the new one blended from the old). Sized once per monitor;
/// <see cref="Update"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class AmbientPass : IDisposable
{
    /// <summary>Desktop pixels per weight texel; the footprint of one decision is twice that.</summary>
    public const int Block = 16;

    // R16_FLOAT is a render target and filterable from feature level 10_0, and keeps the small
    // per-frame steps of the time smoothing that 8 bits would round away.
    private const Format WeightFormat = Format.R16_Float;

    private readonly ID3D11Texture2D[] _textures = new ID3D11Texture2D[2];
    private readonly ID3D11RenderTargetView[] _targets = new ID3D11RenderTargetView[2];
    private readonly ID3D11ShaderResourceView[] _views = new ID3D11ShaderResourceView[2];
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11SamplerState _sampler;
    private readonly ID3D11Buffer _constants;
    private readonly float _desktopWidth;
    private readonly float _desktopHeight;
    private int _latest;

    public AmbientPass(ID3D11Device device, int desktopWidth, int desktopHeight)
    {
        _desktopWidth = desktopWidth;
        _desktopHeight = desktopHeight;
        Width = (desktopWidth + Block - 1) / Block;
        Height = (desktopHeight + Block - 1) / Block;
        for (var i = 0; i < 2; i++)
        {
            _textures[i] = device.CreateTexture2D(new Texture2DDescription(
                WeightFormat, (uint)Width, (uint)Height, arraySize: 1, mipLevels: 1,
                bindFlags: BindFlags.RenderTarget | BindFlags.ShaderResource));
            _targets[i] = device.CreateRenderTargetView(_textures[i]);
            _views[i] = device.CreateShaderResourceView(_textures[i]);
        }

        _vertexShader = device.CreateVertexShader(ShaderSource.Compile("Ambient.hlsl", "VSMain", "vs_4_0").Span);
        _pixelShader = device.CreatePixelShader(ShaderSource.Compile("Ambient.hlsl", "PSMain", "ps_4_0").Span);
        _sampler = device.CreateSamplerState(SamplerDescription.LinearClamp);
        _constants = device.CreateBuffer(new BufferDescription(AmbientConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The weights of the last <see cref="Update"/>, for Magnify.hlsl (t1).</summary>
    public ID3D11ShaderResourceView Weights => _views[_latest];

    /// <summary>
    /// Decides the weights from <paramref name="desktop"/> and blends them into the previous ones.
    /// </summary>
    /// <param name="alpha">Share of the new weights; 1 after a pause, so nothing old shows through.</param>
    public void Update(ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, float alpha, bool protectPhotos)
    {
        var next = 1 - _latest;
        var constants = new AmbientConstants(_desktopWidth, _desktopHeight, alpha, protectPhotos, Block);
        context.UpdateSubresource(constants, _constants);

        // The texture written now was read by the last frame's magnify pass; unbind it first.
        context.PSUnsetShaderResource(1);
        context.OMSetRenderTargets(_targets[next]);
        context.RSSetViewport(0, 0, Width, Height);
        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.PSSetShaderResource(0, desktop);
        context.PSSetShaderResource(1, _views[_latest]);
        context.PSSetSampler(0, _sampler);
        context.PSSetConstantBuffer(0, _constants);
        context.Draw(3, 0);
        context.PSUnsetShaderResource(1);
        _latest = next;
    }

    public void Dispose()
    {
        _constants.Dispose();
        _sampler.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
        for (var i = 0; i < 2; i++)
        {
            _views[i].Dispose();
            _targets[i].Dispose();
            _textures[i].Dispose();
        }
    }
}
