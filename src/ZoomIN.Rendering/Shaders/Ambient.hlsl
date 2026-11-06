// Smart inversion (ADR-0024), step 1 of 2: one texel per BLOCK × BLOCK desktop pixels decides from
// its surroundings whether it is light background (to invert), the luma of that background and
// whether it looks like a photo. Step 2 (PhotoFill.hlsl) fills holes in photos and smooths over time.

cbuffer AmbientConstants : register(b0)
{
    float2 TextureSize;   // desktop texture size in pixels
    float Alpha;          // share of the new weight this frame (1 = no memory); used by PhotoFill.hlsl
    uint ProtectPhotos;   // 1 = colourful, detailed surroundings stay original
    float Block;          // desktop pixels per weight texel
    float3 Padding;
};

Texture2D Desktop : register(t0);
SamplerState LinearSampler : register(s0);

// 4 × 4 taps STEP pixels apart, each between four pixels (bilinear = their mean): a footprint of
// 32 × 32 pixels around the texel centre, so neighbouring texels overlap.
#define TAPS 4
#define STEP 8.0

// A sample counts as light from LIGHT_LOW, fully from LIGHT_HIGH (luma of the sRGB values).
#define LIGHT_LOW 0.55
#define LIGHT_HIGH 0.75

// Share of light samples for a light background. Bold headings have much ink, so one half.
#define LIGHT_MAJORITY 0.5

// A light background is paper: one even tone. Samples within PAPER_TOLERANCE of the lightest one
// count as paper; at least PAPER_SHARE of them are needed. Measured on real captures: text, pages,
// headings and the Control Panel 8–13 of 16; snow in a photo 1–3 of 16 (even where 12 of 16 are
// light), a coloured icon 4 of 16. Light without paper (photos, icons) stays original.
#define PAPER_TOLERANCE 0.04
#define PAPER_SHARE 0.375


// Photos: most of a 64 × 64 surrounding colourful (a sample counts from its chroma) and detailed
// (spread of the colours). Larger than the 32 px of the light/dark decision on purpose: icons
// (up to ~48 px) and coloured link text are no photos – protecting them left grey halos around
// every icon on a white page. Grey text has no chroma; a flat coloured area has no spread.
#define PHOTO_STEP 16.0
#define PHOTO_SHARE 0.5
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

// True if most of the 64 × 64 surrounding of centre is colourful and – for light texels – detailed;
// luma = its mean luma. A dark, colourful area (a blue sky) need not be detailed: dark UI areas are
// grey or black, and for PhotoFill.hlsl a sky is photo around a snowfield, not a dark bar.
bool IsPhoto(float2 centre, bool lightTexel, out float luma)
{
    float colourful = 0;
    float3 sum = 0;
    float sumSquares = 0;
    luma = 0;
    [unroll]
    for (int y = 0; y < TAPS; y++)
    {
        [unroll]
        for (int x = 0; x < TAPS; x++)
        {
            float2 at = centre + (float2(x, y) - (TAPS - 1) * 0.5) * PHOTO_STEP;
            float3 c = Desktop.SampleLevel(LinearSampler, at / TextureSize, 0).rgb;
            float chroma = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
            colourful += smoothstep(CHROMA_LOW, CHROMA_HIGH, chroma);
            sum += c;
            sumSquares += dot(c, c);
            luma += dot(c, Luma);
        }
    }

    const float n = TAPS * TAPS;
    float3 mean = sum / n;
    float spread = sqrt(max(sumSquares / n - dot(mean, mean), 0) / 3);
    luma /= n;
    return colourful >= n * PHOTO_SHARE && (!lightTexel || smoothstep(DETAIL_LOW, DETAIL_HIGH, spread) >= 0.5);
}

// x = light background on paper (1) or not (0), y = luma of that background (Magnify.hlsl keeps
// edges sharp with it), z = looks like a photo (1), w = light but without paper (1: text crowded
// enough, an icon, a frame line – or a photo; PhotoFill.hlsl tells them apart by their neighbours).
float4 PSMain(VertexOutput input) : SV_Target
{
    // SV_Position is the texel centre (x + 0.5); scaled, the taps land on pixel corners.
    float2 centre = input.Position.xy * Block;

    float samples[TAPS * TAPS];
    float light = 0;
    float darkLuma = 0;
    float paper = 0;
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
            samples[y * TAPS + x] = l;
            light += isLight;
            darkLuma += (1 - isLight) * l;
            paper = max(paper, l);
        }
    }

    const float n = TAPS * TAPS;
    float onPaper = 0;
    float paperLuma = 0;
    float otherLuma = 0;
    [unroll]
    for (int i = 0; i < TAPS * TAPS; i++)
    {
        bool isPaper = samples[i] >= paper - PAPER_TOLERANCE;
        onPaper += isPaper ? 1 : 0;
        paperLuma += isPaper ? samples[i] : 0;
        otherLuma += isPaper ? 0 : samples[i];
    }

    // One decision for weight and background: the majority of the samples. Text on a white page keeps
    // a light majority, a dark bar with light text a dark one. A texel on an edge thus decides for
    // the side it mostly covers; Magnify.hlsl gives each pixel the texel whose background matches,
    // so edges stay sharp. A soft share band here left half-inverted grey seams at every edge.
    bool lightMajority = light >= n * LIGHT_MAJORITY;
    bool lightBackground = lightMajority && onPaper >= n * PAPER_SHARE;
    float weight = lightBackground ? 1 : 0;

    // Luma of that background – what Magnify.hlsl matches pixels against: the paper tone; for light
    // without paper (photo, icon) the luma of that content, so its pixels follow this texel and the
    // paper around it the paper texels; otherwise the mean of the dark samples.
    float background = lightBackground ? paperLuma / onPaper
        : lightMajority ? otherLuma / max(n - onPaper, 1)
        : darkLuma / max(n - light, 1e-3);

    // Photos stay original – a decision as well, not a blend (a half-protected texel gives a grey
    // halo). Its background is the luma of the photo, so its pixels follow it in Magnify.hlsl.
    // Dark texels are tested too: PhotoFill.hlsl needs them to see that a white patch (snow, sky)
    // lies inside a photo.
    float photo = 0;
    float photoLuma;
    // A texel with paper is page, never photo: the inside of photos has none (measured), but the
    // 64 px photo test also sees a coloured bar or photo next to the page and would keep it white.
    if (ProtectPhotos != 0 && !lightBackground && IsPhoto(centre, lightMajority, photoLuma))
    {
        photo = 1;
        // The content, not the page around it: otherwise the paper next to a photo (or a coloured
        // bar) would follow this texel and stay white.
        background = !lightMajority ? background : onPaper < n ? otherLuma / (n - onPaper) : photoLuma;
    }

    return float4(weight, background, photo, lightMajority && !lightBackground ? 1 : 0);
}
