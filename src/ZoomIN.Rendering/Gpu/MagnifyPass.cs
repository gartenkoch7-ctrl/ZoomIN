using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Draws the magnified source texture into a render target with one fullscreen triangle.
/// <see cref="Draw"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class MagnifyPass : IDisposable
{
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11SamplerState _sampler;
    private readonly ID3D11SamplerState _pointSampler;
    private readonly ID3D11Buffer _constants;
    private readonly ID3D11Buffer _windows;
    private readonly ID3D11Buffer _enhance;

    // Not readonly: it remembers the last upload. A resting zoom draws the same values each frame.
    private ConstantUpload<MagnifyConstants> _uploaded;

    /// <param name="enhanced">D1 spike, variant A: pixel shader PSEnhanced, reads <see cref="UpdateEnhance"/>.</param>
    public MagnifyPass(ID3D11Device device, bool enhanced = false)
    {
        _vertexShader = device.CreateVertexShader(ShaderSource.Load("Magnify.hlsl", "VSMain", "vs_4_0"));
        _pixelShader = device.CreatePixelShader(enhanced
            ? ShaderSource.Load("Magnify.hlsl", "PSEnhanced", "ps_4_0")
            : ShaderSource.Load("Magnify.hlsl", "PSMain", "ps_4_0"));
        _enhance = device.CreateBuffer(new BufferDescription(EnhanceConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));
        _sampler = device.CreateSamplerState(SamplerDescription.LinearClamp);
        _pointSampler = device.CreateSamplerState(SamplerDescription.PointClamp);
        _constants = device.CreateBuffer(new BufferDescription(MagnifyConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));

        // Starts with no regions, so the monitor matrix applies everywhere until the first update.
        var none = default(WindowConstants);
        _windows = device.CreateBuffer(in none, new BufferDescription((uint)WindowConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));
    }

    /// <summary>D1 spike: sharpness and bold of PSEnhanced; ignored by PSMain.</summary>
    public void UpdateEnhance(ID3D11DeviceContext context, in EnhanceConstants enhance) =>
        context.UpdateSubresource(enhance, _enhance);

    /// <summary>New window regions or palette; call only when they changed (2.5 KB upload).</summary>
    public void UpdateWindows(ID3D11DeviceContext context, in WindowConstants windows) =>
        context.UpdateSubresource(windows, _windows);

    /// <summary>Draws into the whole render target.</summary>
    public void Draw(
        ID3D11DeviceContext context,
        ID3D11ShaderResourceView source,
        ID3D11RenderTargetView target,
        int targetWidth,
        int targetHeight,
        in MagnifyConstants constants,
        ID3D11ShaderResourceView? weights = null) =>
        Draw(context, source, target, 0, 0, targetWidth, targetHeight, constants, weights);

    /// <summary>Draws into the viewport (x, y, width, height) of the render target; the rest is untouched.</summary>
    /// <param name="weights">Smart inversion weights (<see cref="AmbientPass"/>); null if nothing on the monitor is smart.</param>
    public void Draw(
        ID3D11DeviceContext context,
        ID3D11ShaderResourceView source,
        ID3D11RenderTargetView target,
        float x,
        float y,
        float width,
        float height,
        in MagnifyConstants constants,
        ID3D11ShaderResourceView? weights = null)
    {
        _uploaded.Update(context, _constants, constants);

        context.OMSetRenderTargets(target);
        context.RSSetViewport(x, y, width, height);
        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.PSSetShaderResource(0, source);
        if (weights is null)
        {
            context.PSUnsetShaderResource(1);
        }
        else
        {
            context.PSSetShaderResource(1, weights);
        }

        context.PSSetSampler(0, _sampler);
        context.PSSetSampler(1, _pointSampler);
        context.PSSetConstantBuffer(0, _constants);
        context.PSSetConstantBuffer(1, _windows);
        context.PSSetConstantBuffer(2, _enhance);
        context.Draw(3, 0);
    }

    public void Dispose()
    {
        _enhance.Dispose();
        _windows.Dispose();
        _constants.Dispose();
        _pointSampler.Dispose();
        _sampler.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
