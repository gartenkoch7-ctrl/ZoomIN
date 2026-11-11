// Text enhancement of feature D1 (spike, never merged): clamped unsharp mask ("Schärfe") and an
// adaptive luminance min/max filter ("Fett"). Variant B (Enhance.hlsl) runs Enhance() on the result;
// variant A ran it on the source in its first round (too slow) and now only uses the constants,
// RingDirections and EnhanceLuma (EnhanceInPass.hlsli).
// The including shader defines EnhanceTap(uv) first: one bilinear sample of the image it works on.

cbuffer EnhanceConstants : register(b2)
{
    float Sharpness;       // 0–1
    float BoldPx;          // 0–3, in output pixels
    float SharpenReach;    // output pixels per source texel (the zoom); only the post pass uses it
    float EnhancePadding;
    float2 OutputTexel;    // 1 / output size in pixels
    float2 EnhancePadding2;
};

// The decision "is the thin feature dark or light?" reads the surroundings this many source texels
// away: beyond a text stroke (1–2 texels), inside the paper around it.
#define DECIDE_REACH 3.0

// Below this luma range between the bold taps there is no stroke to widen (flat areas, soft photos).
#define BOLD_MIN_CONTRAST 0.15

static const float2 RingDirections[8] =
{
    float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
    float2(0.7071, 0.7071), float2(-0.7071, 0.7071), float2(0.7071, -0.7071), float2(-0.7071, -0.7071),
};

float EnhanceLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// Unsharp mask against the cross of four neighbours one source texel away, clamped to their range:
// an edge gets steeper but never overshoots, so neither a light nor a dark halo can appear.
float3 Sharpen(float2 uv, float3 c, float2 texel)
{
    float3 n0 = EnhanceTap(uv + float2(texel.x, 0));
    float3 n1 = EnhanceTap(uv - float2(texel.x, 0));
    float3 n2 = EnhanceTap(uv + float2(0, texel.y));
    float3 n3 = EnhanceTap(uv - float2(0, texel.y));
    float3 blur = (n0 + n1 + n2 + n3) * 0.25;
    float3 lo = min(c, min(min(n0, n1), min(n2, n3)));
    float3 hi = max(c, max(max(n0, n1), max(n2, n3)));
    return clamp(c + (c - blur) * (Sharpness * 2.0), lo, hi);
}

// Widens the thin feature by BoldPx: takes the darkest tap where the surroundings are mostly light
// (dark text on paper) and the lightest where they are mostly dark (light text, also after
// inverting). Deciding by the surroundings, not by a fixed direction, makes the result the same
// before and after any inverting colour matrix. The tap keeps its colour, so coloured text stays.
float3 Bolden(float2 uv, float3 c, float2 texel, float2 px)
{
    float3 darkest = c;
    float3 lightest = c;
    float darkLuma = EnhanceLuma(c);
    float lightLuma = darkLuma;
    float around = 0;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float3 tap = EnhanceTap(uv + RingDirections[i] * px * BoldPx);
        float luma = EnhanceLuma(tap);
        if (luma < darkLuma) { darkLuma = luma; darkest = tap; }
        if (luma > lightLuma) { lightLuma = luma; lightest = tap; }
        around += EnhanceLuma(EnhanceTap(uv + RingDirections[i] * texel * DECIDE_REACH));
    }

    if (lightLuma - darkLuma < BOLD_MIN_CONTRAST)
    {
        return c;
    }

    // Paper lighter than the middle of the local range: the stroke is the dark side.
    return around * 0.125 >= (darkLuma + lightLuma) * 0.5 ? darkest : lightest;
}

// texel = one source texel, px = one output pixel, both in the UV space of EnhanceTap.
float3 Enhance(float2 uv, float3 c, float2 texel, float2 px)
{
    if (Sharpness > 0)
    {
        c = Sharpen(uv, c, texel);
    }

    if (BoldPx > 0)
    {
        c = Bolden(uv, c, texel, px);
    }

    return c;
}
