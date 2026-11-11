using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// D1 spike (never merged), variant B: an offscreen target of monitor size for the magnify pass and
/// a second pass (Enhance.hlsl) that sharpens and bolds it into the real target. The target is
/// created once per size, like DesktopLayer would on start/resize; <see cref="Draw"/> does not allocate.
/// </summary>
internal sealed class EnhancePass : IDisposable
{
    private readonly ID3D11Texture2D _offscreen;
    private readonly ID3D11ShaderResourceView _offscreenView;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11SamplerState _sampler;
    private readonly ID3D11Buffer _constants;
    private readonly int _width;
    private readonly int _height;
    private ConstantUpload<EnhanceConstants> _uploaded;

    public EnhancePass(ID3D11Device device, int width, int height)
    {
        _width = width;
        _height = height;
        _offscreen = device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height, arraySize: 1, mipLevels: 1,
            bindFlags: BindFlags.RenderTarget | BindFlags.ShaderResource));
        Target = device.CreateRenderTargetView(_offscreen);
        _offscreenView = device.CreateShaderResourceView(_offscreen);
        _vertexShader = device.CreateVertexShader(ShaderSource.Load("Magnify.hlsl", "VSMain", "vs_4_0"));
        _pixelShader = device.CreatePixelShader(ShaderSource.Load("Enhance.hlsl", "PSMain", "ps_4_0"));
        _sampler = device.CreateSamplerState(SamplerDescription.LinearClamp);
        _constants = device.CreateBuffer(new BufferDescription(EnhanceConstants.SizeInBytes, BindFlags.ConstantBuffer, ResourceUsage.Default));
    }

    /// <summary>Where the magnify pass draws instead of the swapchain.</summary>
    public ID3D11RenderTargetView Target { get; }

    /// <summary>Enhances the offscreen image into <paramref name="target"/> (same size).</summary>
    public void Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, in EnhanceConstants constants)
    {
        _uploaded.Update(context, _constants, constants);
        context.OMSetRenderTargets(target);
        context.RSSetViewport(0, 0, _width, _height);
        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.PSSetShaderResource(0, _offscreenView);
        context.PSSetSampler(0, _sampler);
        context.PSSetConstantBuffer(2, _constants);
        context.Draw(3, 0);

        // The next frame renders into the offscreen texture again; it must not stay bound as input.
        context.PSUnsetShaderResource(0);
    }

    public void Dispose()
    {
        _constants.Dispose();
        _sampler.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
        _offscreenView.Dispose();
        Target.Dispose();
        _offscreen.Dispose();
    }
}
