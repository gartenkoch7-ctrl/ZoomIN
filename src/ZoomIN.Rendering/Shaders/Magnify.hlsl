// Magnifies a rectangle of the captured desktop into the viewport, optionally with a frame,
// then applies the colour matrix (filter, contrast, brightness). One pass for everything.

cbuffer MagnifyConstants : register(b0)
{
    float2 SourceOffset; // top-left of the source rectangle, in desktop texture UV space
    float2 SourceScale;  // size of the source rectangle, in desktop texture UV space
    float2 InnerOffset;  // top-left of the target inside the viewport, in viewport UV space
    float2 InnerScale;   // size of the target inside the viewport, in viewport UV space
    float4 FrameColor;   // premultiplied colour of the frame around the target
    float4 ColorRow0;    // colour matrix row for red (xyz) and its offset (w)
    float4 ColorRow1;
    float4 ColorRow2;
    float2 TextureSize;  // desktop texture size in pixels
    uint Smoothing;      // 0 = text (Catmull-Rom), 1 = soft (bilinear), 2 = pixel (nearest)
    float Padding;
};

Texture2D Desktop : register(t0);
SamplerState LinearSampler : register(s0);
SamplerState PointSampler : register(s1);

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

// Catmull-Rom bicubic with 9 bilinear taps instead of 16 point taps. Sharper than bilinear,
// which keeps text edges crisp at high zoom.
float3 SampleCatmullRom(float2 uv)
{
    float2 samplePos = uv * TextureSize;
    float2 texPos1 = floor(samplePos - 0.5) + 0.5;
    float2 f = samplePos - texPos1;

    float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    float2 w3 = f * f * (-0.5 + 0.5 * f);

    float2 w12 = w1 + w2;
    float2 texPos0 = (texPos1 - 1) / TextureSize;
    float2 texPos3 = (texPos1 + 2) / TextureSize;
    float2 texPos12 = (texPos1 + w2 / w12) / TextureSize;

    float3 result = 0;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos0.x, texPos0.y), 0).rgb * w0.x * w0.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos12.x, texPos0.y), 0).rgb * w12.x * w0.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos3.x, texPos0.y), 0).rgb * w3.x * w0.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos0.x, texPos12.y), 0).rgb * w0.x * w12.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos12.x, texPos12.y), 0).rgb * w12.x * w12.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos3.x, texPos12.y), 0).rgb * w3.x * w12.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos0.x, texPos3.y), 0).rgb * w0.x * w3.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos12.x, texPos3.y), 0).rgb * w12.x * w3.y;
    result += Desktop.SampleLevel(LinearSampler, float2(texPos3.x, texPos3.y), 0).rgb * w3.x * w3.y;
    return result;
}

float3 SampleDesktop(float2 uv)
{
    if (Smoothing == 0)
    {
        return SampleCatmullRom(uv);
    }

    return Smoothing == 1
        ? Desktop.SampleLevel(LinearSampler, uv, 0).rgb
        : Desktop.SampleLevel(PointSampler, uv, 0).rgb;
}

float4 PSMain(VertexOutput input) : SV_Target
{
    float2 inner = (input.Uv - InnerOffset) / InnerScale;
    if (any(inner < 0) || any(inner > 1))
    {
        return FrameColor;
    }

    float3 c = SampleDesktop(SourceOffset + inner * SourceScale);
    float3 filtered = saturate(float3(
        dot(ColorRow0.xyz, c) + ColorRow0.w,
        dot(ColorRow1.xyz, c) + ColorRow1.w,
        dot(ColorRow2.xyz, c) + ColorRow2.w));

    // The overlay is premultiplied-alpha; the desktop is opaque.
    return float4(filtered, 1);
}
