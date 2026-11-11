// D1 spike, variant A (second round): enhancement inside Magnify.hlsl at the lowest tap count.
// The first round ran Enhance.hlsli on the source (Catmull-Rom 9 + 20 bilinear taps): 0.14 ms extra
// at 1080p on an RTX 3060 Ti, three times the budget. This one re-uses the 9 bicubic taps:
// - Schärfe = a sharper cubic (Keys a from -0.5 = Catmull-Rom down to -1.5), clamped to the range
//   of its own taps, so it cannot ring into a halo. No extra taps.
// - Fett = 4 extra taps on a cross BoldPx output pixels away; light or dark side decided by the
//   mean of the 9 bicubic taps (about 4 × 4 source texels around the pixel). 4 extra taps.
// Included by Magnify.hlsl after Enhance.hlsli; needs Desktop, LinearSampler and TextureSize.

// Keys cubic for |x| <= 1 and for 1 < |x| < 2; a = -0.5 is Catmull-Rom.
float2 KeysNear(float2 x, float a) { return ((a + 2) * x - (a + 3)) * x * x + 1; }
float2 KeysFar(float2 x, float a) { return ((a * x - 5 * a) * x + 8 * a) * x - 4 * a; }

float3 SampleSharpBicubic(float2 uv, float a, out float meanLuma)
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
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        result += taps[i] * weights[i];
        lo = min(lo, taps[i]);
        hi = max(hi, taps[i]);
        meanLuma += EnhanceLuma(taps[i]);
    }

    meanLuma /= 9;
    return clamp(result, lo, hi);
}

float3 EnhanceInPass(float2 uv, float2 px)
{
    float meanLuma;
    float3 c = SampleSharpBicubic(uv, -0.5 - Sharpness, meanLuma);
    if (BoldPx <= 0)
    {
        return c;
    }

    float3 darkest = c;
    float3 lightest = c;
    float darkLuma = EnhanceLuma(c);
    float lightLuma = darkLuma;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float3 tap = EnhanceTap(uv + RingDirections[i] * px * BoldPx);
        float luma = EnhanceLuma(tap);
        if (luma < darkLuma) { darkLuma = luma; darkest = tap; }
        if (luma > lightLuma) { lightLuma = luma; lightest = tap; }
    }

    if (lightLuma - darkLuma < BOLD_MIN_CONTRAST)
    {
        return c;
    }

    return meanLuma >= (darkLuma + lightLuma) * 0.5 ? darkest : lightest;
}
