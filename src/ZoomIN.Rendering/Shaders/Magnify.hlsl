// Magnifies a rectangle of the captured desktop onto the whole render target.
// Single pass: later milestones add colour filters and highlights here (M5).

cbuffer MagnifyConstants : register(b0)
{
    float2 SourceOffset; // top-left of the source rectangle, in texture UV space
    float2 SourceScale;  // size of the source rectangle, in texture UV space
};

Texture2D Desktop : register(t0);
SamplerState LinearSampler : register(s0);

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

// Fullscreen triangle generated from the vertex id: no vertex or index buffer needed.
VertexOutput VSMain(uint id : SV_VertexID)
{
    VertexOutput output;
    output.Uv = float2((id << 1) & 2, id & 2);
    output.Position = float4(output.Uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return output;
}

float4 PSMain(VertexOutput input) : SV_Target
{
    float2 uv = SourceOffset + input.Uv * SourceScale;

    // The overlay is premultiplied-alpha; the desktop is opaque.
    return float4(Desktop.Sample(LinearSampler, uv).rgb, 1);
}
