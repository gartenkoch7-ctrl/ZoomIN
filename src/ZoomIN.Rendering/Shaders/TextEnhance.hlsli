// "Schärfe" and "Fett" (M18, D1), included by Magnify.hlsl for PSEnhanced only: PSMain stays as it
// was, so with both sliders at 0 nothing costs more. Runs on the source, before own schemes, the
// colour matrix and the smart inversion, so the result is the same under every filter (spike D1:
// after the smart inversion it widened the seams of the inverted areas). Needs Desktop,
// LinearSampler, TextureSize, TexUv, Sharpness and BoldPx from Magnify.hlsl and Luma from SmartWeight.hlsli.
// Cost: the 9 bicubic taps of PSMain plus 4 for bold.

// Below this luma range around the pixel there is no stroke to widen (flat areas, soft gradients).
#define BOLD_MIN_CONTRAST 0.15

// The bold taps reach at most this many source texels. Further out the 4 taps skipped the edge of a
// one-texel stroke and drew a second, ghost copy of it beside a lighter gap (seen at 2× with 3 px);
// so at low zoom the widening stops at half a texel (2× at most 1 px, 4× at most 2 px).
#define BOLD_MAX_TEXELS 0.5

// Share of a texel that the remapped position of "Schärfe" skips at 100 %: two thirds.
#define SHARP_NARROWING 0.6667

// Photo content (risk 2): at full strength bold turned hard photo edges into stairs and sharpness
// into visible pixel blocks. A tap is colourful from its chroma (thresholds of the photo test in
// Ambient.hlsl). The four corner taps of the bicubic footprint (4 × 4 texels) decide: up to two
// colourful ones keep both, with three or four they are off. Grey text and its ClearType fringes (a
// minority) keep them; text on a strongly coloured area loses them too (documented trade-off).
#define CHROMA_LOW 0.10
#define CHROMA_HIGH 0.25
#define PHOTO_FROM 2.0
#define PHOTO_FULL 3.0

static const float2 BoldDirections[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };

float Chroma(float3 c) { return max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b)); }

float Colourful(float3 c) { return smoothstep(CHROMA_LOW, CHROMA_HIGH, Chroma(c)); }

float3 EnhanceTap(float2 uv) { return Desktop.SampleLevel(LinearSampler, TexUv(uv), 0).rgb; }

// "Schärfe": Catmull-Rom with the same 9 bilinear taps, but evaluated at a remapped position between
// two texels: the middle of the way is crossed faster and the texel centres widen into small
// plateaus, so an edge between two texels gets steeper (at 100 % three times, a third of a texel
// wide). Costs nothing extra. The spike's sharper Keys kernel (a below -0.5) was dropped: its slope
// at an edge is 1.5 + a/2, so once clamped it made edges softer and strokes only darker.
// The remap only moves where a transition happens, so the slight overshoot of Catmull-Rom stays as
// it is today and no new halo appears. Not clamped to the range of the taps: the middle taps are
// blends of two texels, so that range cut into real Catmull-Rom detail and blurred every edge.
// Kept as a copy of SampleCatmullRom (Magnify.hlsl), not shared: PSMain must stay exactly as it was.
// Change both together. Also returns the luma range and mean of the taps and how much the
// surroundings look like a photo.
float3 SampleSharpBicubic(float2 uv, out float loLuma, out float hiLuma, out float meanLuma, out float photo)
{
    float2 samplePos = uv * TextureSize;
    float2 texPos1 = floor(samplePos - 0.5) + 0.5;
    float2 p0 = (texPos1 - 1) / TextureSize;
    float2 p3 = (texPos1 + 2) / TextureSize;

    // The corners sit on texel centres whatever the remap, so they decide it before the other taps.
    float3 c00 = EnhanceTap(float2(p0.x, p0.y));
    float3 c30 = EnhanceTap(float2(p3.x, p0.y));
    float3 c03 = EnhanceTap(float2(p0.x, p3.y));
    float3 c33 = EnhanceTap(float2(p3.x, p3.y));
    photo = smoothstep(PHOTO_FROM, PHOTO_FULL, Colourful(c00) + Colourful(c30) + Colourful(c03) + Colourful(c33));

    float narrowing = SHARP_NARROWING * Sharpness * (1 - photo);
    float2 f = saturate((samplePos - texPos1 - 0.5) / (1 - narrowing) + 0.5);
    float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    float2 w3 = f * f * (-0.5 + 0.5 * f);
    float2 w12 = w1 + w2;
    float2 p12 = (texPos1 + w2 / w12) / TextureSize;

    float3 taps[9] =
    {
        c00, EnhanceTap(float2(p12.x, p0.y)), c30,
        EnhanceTap(float2(p0.x, p12.y)), EnhanceTap(float2(p12.x, p12.y)), EnhanceTap(float2(p3.x, p12.y)),
        c03, EnhanceTap(float2(p12.x, p3.y)), c33,
    };
    float weights[9] =
    {
        w0.x * w0.y, w12.x * w0.y, w3.x * w0.y,
        w0.x * w12.y, w12.x * w12.y, w3.x * w12.y,
        w0.x * w3.y, w12.x * w3.y, w3.x * w3.y,
    };

    float3 result = 0;
    loLuma = 1;
    hiLuma = 0;
    meanLuma = 0;
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        result += taps[i] * weights[i];
        float luma = Luma(taps[i]);
        loLuma = min(loLuma, luma);
        hiLuma = max(hiLuma, luma);
        meanLuma += luma;
    }

    meanLuma /= 9;
    return result;
}
// px = one output pixel in source UV. Returns the enhanced source colour at uv.
float3 EnhanceText(float2 uv, float2 px)
{
    float loLuma, hiLuma, meanLuma, photo;
    float3 c = SampleSharpBicubic(uv, loLuma, hiLuma, meanLuma, photo);
    // Off, or a photo (risk 2): the cross taps would only be thrown away below.
    if (BoldPx <= 0 || photo >= 1)
    {
        return c;
    }

    // "Fett": a minimum or maximum filter on luma over a cross BoldPx output pixels wide.
    float2 reach = min(px * BoldPx, BOLD_MAX_TEXELS / TextureSize);
    // Catmull-Rom may overshoot past white or black at an edge; the scaling below needs 0..1 (an
    // overshoot of 1.02 divided by 1 - 1.02 and turned the pixel black). The output clips it anyway.
    c = saturate(c);
    float own = Luma(c);
    float darkLuma = own;
    float lightLuma = own;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float luma = Luma(EnhanceTap(uv + BoldDirections[i] * reach));
        darkLuma = min(darkLuma, luma);
        lightLuma = max(lightLuma, luma);
    }

    // The 9 taps alone miss a stroke one texel away: the bilinear trick blends it in with a small
    // weight. The cross taps alone miss it at a small BoldPx. Together they see it.
    loLuma = min(loLuma, darkLuma);
    hiLuma = max(hiLuma, lightLuma);
    if (hiLuma - loLuma < BOLD_MIN_CONTRAST)
    {
        return c;
    }

    // The stroke is the minority: mostly light surroundings (about 4 × 4 texels, the 9 taps) widen
    // the dark side, mostly dark ones the light side. Decided on the source, so it is the same
    // before and after any inverting filter.
    float target = meanLuma >= (loLuma + hiLuma) * 0.5 ? darkLuma : lightLuma;

    // Risk 1: only the luma of the darkest or lightest tap is taken; the pixel keeps its own hue.
    // Taking the whole tap made ClearType fringes more colourful (the extreme tap is often one).
    // Scaled towards black or white, so the hue stays and the saturation can only drop; the two
    // cases mirror each other, as inverting does.
    float3 bold = target < own
        ? c * (target / max(own, 1e-4))
        : 1 - (1 - c) * ((1 - target) / max(1 - own, 1e-4));
    return lerp(bold, c, photo);
}
