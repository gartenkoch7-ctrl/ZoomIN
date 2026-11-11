// "Schärfe" and "Fett" (M18, D1), included by Magnify.hlsl for PSEnhanced only: PSMain stays as it
// was, so with both sliders at 0 nothing costs more. Runs on the source, before own schemes, the
// colour matrix and the smart inversion, so the result is the same under every filter (spike D1:
// after the smart inversion it widened the seams of the inverted areas). Needs Desktop,
// LinearSampler, TextureSize, Sharpness and BoldPx from Magnify.hlsl and Luma from SmartWeight.hlsli.
// Budget (spike, RTX 3060 Ti, 1080p): 9 bicubic taps as before plus 4 for bold, about 0.03 ms.

// Below this luma range between the bold taps there is no stroke to widen (flat areas, soft photos).
#define BOLD_MIN_CONTRAST 0.15

// Photo content (risk 2): bold widened hard photo edges into stairs at 3 px. A tap is colourful from
// its chroma (same thresholds as the photo test of Ambient.hlsl); with most of the 9 taps colourful
// the pixel counts as photo and bold fades out. Grey text and its ClearType fringes (a minority of
// the taps) stay bold.
#define CHROMA_LOW 0.10
#define CHROMA_HIGH 0.25
#define PHOTO_FROM 4.0
#define PHOTO_FULL 7.0

static const float2 BoldDirections[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };

float Chroma(float3 c) { return max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b)); }

// Keys cubic for |x| <= 1 and for 1 < |x| < 2; a = -0.5 is Catmull-Rom (SampleCatmullRom).
float2 KeysNear(float2 x, float a) { return ((a + 2) * x - (a + 3)) * x * x + 1; }
float2 KeysFar(float2 x, float a) { return ((a * x - 5 * a) * x + 8 * a) * x - 4 * a; }

float3 EnhanceTap(float2 uv) { return Desktop.SampleLevel(LinearSampler, uv, 0).rgb; }

// "Schärfe": a sharper bicubic, Keys a from -0.5 (Catmull-Rom) down to -1.5, with the same 9
// bilinear taps. A sharpening filter one output pixel wide did nearly nothing at 8× (the image is
// smooth at that scale); this one sharpens at the scale of a source texel and costs nothing. The
// result is clamped to the range of its taps: an edge gets steeper but never overshoots, so no
// light or dark halo appears. Also returns the mean luma and the colourful share of the taps.
float3 SampleSharpBicubic(float2 uv, float a, out float meanLuma, out float colourful)
{
    float2 samplePos = uv * TextureSize;
    float2 texPos1 = floor(samplePos - 0.5) + 0.5;
    float2 f = samplePos - texPos1;

    // Both middle weights stay >= 0 for a >= -1.5, which the 9-tap bilinear trick needs.
    float2 w0 = KeysFar(1 + f, a);
    float2 w1 = KeysNear(f, a);
    float2 w2 = KeysNear(1 - f, a);
    float2 w3 = KeysFar(2 - f, a);

    float2 w12 = w1 + w2;
    float2 p0 = (texPos1 - 1) / TextureSize;
    float2 p3 = (texPos1 + 2) / TextureSize;
    float2 p12 = (texPos1 + w2 / w12) / TextureSize;

    float3 taps[9] =
    {
        EnhanceTap(float2(p0.x, p0.y)), EnhanceTap(float2(p12.x, p0.y)), EnhanceTap(float2(p3.x, p0.y)),
        EnhanceTap(float2(p0.x, p12.y)), EnhanceTap(float2(p12.x, p12.y)), EnhanceTap(float2(p3.x, p12.y)),
        EnhanceTap(float2(p0.x, p3.y)), EnhanceTap(float2(p12.x, p3.y)), EnhanceTap(float2(p3.x, p3.y)),
    };
    float weights[9] =
    {
        w0.x * w0.y, w12.x * w0.y, w3.x * w0.y,
        w0.x * w12.y, w12.x * w12.y, w3.x * w12.y,
        w0.x * w3.y, w12.x * w3.y, w3.x * w3.y,
    };

    float3 result = 0;
    float3 lo = taps[4];
    float3 hi = taps[4];
    meanLuma = 0;
    colourful = 0;
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        result += taps[i] * weights[i];
        lo = min(lo, taps[i]);
        hi = max(hi, taps[i]);
        meanLuma += Luma(taps[i]);
        colourful += smoothstep(CHROMA_LOW, CHROMA_HIGH, Chroma(taps[i]));
    }

    meanLuma /= 9;
    return clamp(result, lo, hi);
}

// px = one output pixel in source UV. Returns the enhanced source colour at uv.
float3 EnhanceText(float2 uv, float2 px)
{
    float meanLuma;
    float colourful;
    float3 c = SampleSharpBicubic(uv, -0.5 - Sharpness, meanLuma, colourful);
    if (BoldPx <= 0)
    {
        return c;
    }

    // "Fett": a minimum or maximum filter on luma over a cross BoldPx output pixels wide.
    float own = Luma(c);
    float darkLuma = own;
    float lightLuma = own;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float luma = Luma(EnhanceTap(uv + BoldDirections[i] * px * BoldPx));
        darkLuma = min(darkLuma, luma);
        lightLuma = max(lightLuma, luma);
    }

    if (lightLuma - darkLuma < BOLD_MIN_CONTRAST)
    {
        return c;
    }

    // The stroke is the minority: mostly light surroundings (about 4 × 4 texels, the 9 taps) widen
    // the dark side, mostly dark ones the light side. Decided on the source, so it is the same
    // before and after any inverting filter.
    float target = meanLuma >= (darkLuma + lightLuma) * 0.5 ? darkLuma : lightLuma;

    // Risk 1: only the luma of the darkest or lightest tap is taken; the pixel keeps its own hue.
    // Taking the whole tap made ClearType fringes more colourful (the extreme tap is often one).
    float3 bold = saturate(c + (target - own));
    return lerp(bold, c, smoothstep(PHOTO_FROM, PHOTO_FULL, colourful));
}
