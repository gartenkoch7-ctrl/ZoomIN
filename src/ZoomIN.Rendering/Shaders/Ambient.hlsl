// Smart inversion (ADR-0024), step 1: how much each spot of the desktop should be inverted. One
// texel per BLOCK × BLOCK desktop pixels holds a weight 0–1, decided from the surroundings: light
// surroundings → 1, dark bars and photos → 0. Blended with the previous weight over time, so a
// spot near the threshold fades instead of flickering while content scrolls. Magnify.hlsl reads it
// bilinearly in desktop UV, which also smooths the edges between light and dark areas.

cbuffer AmbientConstants : register(b0)
{
    float2 TextureSize;   // desktop texture size in pixels
    float Alpha;          // share of the new weight this frame (1 = no memory)
    uint ProtectPhotos;   // 1 = colourful, detailed surroundings stay original
    float Block;          // desktop pixels per weight texel
    float3 Padding;
};

Texture2D Desktop : register(t0);
Texture2D Previous : register(t1);
SamplerState LinearSampler : register(s0);

// 4 × 4 taps STEP pixels apart, each between four pixels (bilinear = their mean): a footprint of
// 32 × 32 pixels around the texel centre, so neighbouring texels overlap.
#define TAPS 4
#define STEP 8.0

// A sample counts as light from LIGHT_LOW, fully from LIGHT_HIGH (luma of the sRGB values).
#define LIGHT_LOW 0.55
#define LIGHT_HIGH 0.75

// Share of light samples from which a spot is inverted at all, and fully. Text on a white page
// keeps a high share; a dark bar with light text a low one.
#define SHARE_LOW 0.45
#define SHARE_HIGH 0.75

// Photos: colourful (mean chroma) and detailed (spread of the colours). Grey text on white has
// spread but no chroma; a flat coloured button has chroma but no spread.
#define CHROMA_LOW 0.10
#define CHROMA_HIGH 0.25
#define DETAIL_LOW 0.06
#define DETAIL_HIGH 0.15

static const float3 Luma = float3(0.2126, 0.7152, 0.0722);

struct VertexOutput
{
    float4 Position : SV_Position;
};

VertexOutput VSMain(uint id : SV_VertexID)
{
    VertexOutput output;
    float2 uv = float2((id << 1) & 2, id & 2);
    output.Position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return output;
}

float PSMain(VertexOutput input) : SV_Target
{
    // SV_Position is the texel centre (x + 0.5); scaled, the taps land on pixel corners.
    float2 centre = input.Position.xy * Block;

    float light = 0;
    float3 sum = 0;
    float sumSquares = 0;
    float chroma = 0;
    [unroll]
    for (int y = 0; y < TAPS; y++)
    {
        [unroll]
        for (int x = 0; x < TAPS; x++)
        {
            float2 at = centre + (float2(x, y) - (TAPS - 1) * 0.5) * STEP;
            float3 c = Desktop.SampleLevel(LinearSampler, at / TextureSize, 0).rgb;
            light += smoothstep(LIGHT_LOW, LIGHT_HIGH, dot(c, Luma));
            sum += c;
            sumSquares += dot(c, c);
            chroma += max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
        }
    }

    const float n = TAPS * TAPS;
    float3 mean = sum / n;
    float detail = sqrt(max(sumSquares / n - dot(mean, mean), 0) / 3);
    float weight = smoothstep(SHARE_LOW, SHARE_HIGH, light / n);
    if (ProtectPhotos != 0)
    {
        weight *= 1 - smoothstep(CHROMA_LOW, CHROMA_HIGH, chroma / n) * smoothstep(DETAIL_LOW, DETAIL_HIGH, detail);
    }

    // After a pause the old texture is stale or never written (may even hold NaN): not read at all.
    if (Alpha >= 1)
    {
        return weight;
    }

    float previous = Previous.Load(int3(input.Position.xy, 0)).r;
    return lerp(previous, weight, Alpha);
}
