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

// x = inversion weight, y = luma of the background there (Magnify.hlsl uses it to keep edges sharp).
float2 PSMain(VertexOutput input) : SV_Target
{
    // SV_Position is the texel centre (x + 0.5); scaled, the taps land on pixel corners.
    float2 centre = input.Position.xy * Block;

    float light = 0;
    float lightLuma = 0;
    float darkLuma = 0;
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
            float l = dot(c, Luma);
            float isLight = smoothstep(LIGHT_LOW, LIGHT_HIGH, l);
            light += isLight;
            lightLuma += isLight * l;
            darkLuma += (1 - isLight) * l;
            sum += c;
            sumSquares += dot(c, c);
            chroma += max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
        }
    }

    const float n = TAPS * TAPS;
    float3 mean = sum / n;
    float detail = sqrt(max(sumSquares / n - dot(mean, mean), 0) / 3);

    // One decision for weight and background: the majority of the samples. Text on a white page keeps
    // a light majority, a dark bar with light text a dark one. A texel on an edge thus decides for
    // the side it mostly covers; Magnify.hlsl gives each pixel the texel whose background matches,
    // so edges stay sharp. A soft share band here left half-inverted grey seams at every edge.
    bool lightBackground = light >= n * 0.5;
    float weight = lightBackground ? 1 : 0;

    // Luma of that background: the mean of the majority (light or dark samples), not of everything –
    // text barely moves it, and a texel on an edge reports the side it decides for.
    float background = lightBackground ? lightLuma / max(light, 1e-3) : darkLuma / max(n - light, 1e-3);
    if (ProtectPhotos != 0)
    {
        weight *= 1 - smoothstep(CHROMA_LOW, CHROMA_HIGH, chroma / n) * smoothstep(DETAIL_LOW, DETAIL_HIGH, detail);
    }

    // After a pause the old texture is stale or never written (may even hold NaN): not read at all.
    float2 now = float2(weight, background);
    if (Alpha >= 1)
    {
        return now;
    }

    float2 previous = Previous.Load(int3(input.Position.xy, 0)).rg;
    return lerp(previous, now, Alpha);
}
