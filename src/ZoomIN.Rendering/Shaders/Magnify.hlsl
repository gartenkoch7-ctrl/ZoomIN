// Magnifies a rectangle of the captured desktop into the viewport, optionally with a frame.
// The viewport covers the target plus its frame; the "inner" rectangle is the target itself.
// Single pass: later milestones add colour filters and highlights here (M5).

cbuffer MagnifyConstants : register(b0)
{
    float2 SourceOffset; // top-left of the source rectangle, in desktop texture UV space
    float2 SourceScale;  // size of the source rectangle, in desktop texture UV space
    float2 InnerOffset;  // top-left of the target inside the viewport, in viewport UV space
    float2 InnerScale;   // size of the target inside the viewport, in viewport UV space
    float4 FrameColor;   // premultiplied colour of the frame around the target
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
    float2 inner = (input.Uv - InnerOffset) / InnerScale;
    if (any(inner < 0) || any(inner > 1))
    {
        return FrameColor;
    }

    float2 uv = SourceOffset + inner * SourceScale;

    // The overlay is premultiplied-alpha; the desktop is opaque.
    return float4(Desktop.Sample(LinearSampler, uv).rgb, 1);
}
