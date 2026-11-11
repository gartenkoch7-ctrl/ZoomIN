// D1 spike, variant B: text enhancement as its own pass. Magnify.hlsl renders into an offscreen
// target of monitor size; this pass reads it and writes the enhanced result into the swapchain.
// Works on the final colours (after colour matrix and smart inversion). Vertex shader: Magnify VSMain.

Texture2D Magnified : register(t0);
SamplerState LinearSampler : register(s0);

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

float3 EnhanceTap(float2 uv)
{
    return Magnified.SampleLevel(LinearSampler, uv, 0).rgb;
}

#include "Enhance.hlsli"

float4 PSMain(VertexOutput input) : SV_Target
{
    float3 c = EnhanceTap(input.Uv);

    // One source texel is SharpenReach output pixels here: the image is already magnified.
    return float4(Enhance(input.Uv, c, OutputTexel * SharpenReach, OutputTexel), 1);
}
