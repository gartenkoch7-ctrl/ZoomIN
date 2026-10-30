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

    public MagnifyPass(ID3D11Device device)
    {
        _vertexShader = device.CreateVertexShader(ShaderSource.Compile("Magnify.hlsl", "VSMain", "vs_4_0").Span);
        _pixelShader = device.CreatePixelShader(ShaderSource.Compile("Magnify.hlsl", "PSMain", "ps_4_0").Span);
        _sampler = device.CreateSamplerState(SamplerDescription.LinearClamp);
        _pointSampler = device.CreateSamplerState(SamplerDescription.PointClamp);
        _constants = device.CreateBuffer(new BufferDescription(MagnifyConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));
    }

    /// <summary>Draws into the whole render target.</summary>
    public void Draw(
        ID3D11DeviceContext context,
        ID3D11ShaderResourceView source,
        ID3D11RenderTargetView target,
        int targetWidth,
        int targetHeight,
        in MagnifyConstants constants) =>
        Draw(context, source, target, 0, 0, targetWidth, targetHeight, constants);

    /// <summary>Draws into the viewport (x, y, width, height) of the render target; the rest is untouched.</summary>
    public void Draw(
        ID3D11DeviceContext context,
        ID3D11ShaderResourceView source,
        ID3D11RenderTargetView target,
        float x,
        float y,
        float width,
        float height,
        in MagnifyConstants constants)
    {
        context.UpdateSubresource(constants, _constants);

        context.OMSetRenderTargets(target);
        context.RSSetViewport(x, y, width, height);
        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.PSSetShaderResource(0, source);
        context.PSSetSampler(0, _sampler);
        context.PSSetSampler(1, _pointSampler);
        context.PSSetConstantBuffer(0, _constants);
        context.Draw(3, 0);
    }

    public void Dispose()
    {
        _constants.Dispose();
        _pointSampler.Dispose();
        _sampler.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
