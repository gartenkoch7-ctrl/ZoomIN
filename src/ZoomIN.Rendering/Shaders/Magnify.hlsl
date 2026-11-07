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
    uint MonitorSmart;   // 1 = the monitor matrix is the smart inversion (ADR-0024)
};

// Filter per window (ADR-0020): window rectangles front to back, each with a palette entry. The
// first rectangle holding the source pixel wins, so windows above hide the ones below; pixels in
// none of them keep the monitor's matrix (ColorRow0–2). Updated only when windows or filters change.
#define MAX_REGIONS 64
#define PALETTE_SIZE 10

// Palette entries with a fixed meaning (WindowPalette): the monitor's matrix, original colours.
#define MONITOR_ENTRY 0
#define ORIGINAL_ENTRY 1

cbuffer WindowConstants : register(b1)
{
    uint RegionCount;
    uint SmartEntry;                       // palette entry of the smart inversion (ADR-0024)
    float2 RegionPadding;
    float4 RegionRects[MAX_REGIONS];       // left, top, right, bottom in desktop texture UV space
    float4 RegionEntries[MAX_REGIONS];     // x = palette entry
    float4 Palette[PALETTE_SIZE * 3];      // three colour matrix rows per entry
};

Texture2D Desktop : register(t0);

// Smart inversion: how much each spot is inverted, 0–1, in desktop UV (Ambient.hlsl).
Texture2D Weights : register(t1);
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

// How close a pixel's luma must be to a texel's background luma to follow that texel (edge sharpness).
#define EDGE_SIGMA 0.12

// Desktop pixels per weight texel; must match AmbientPass.Block (checked by a test).
#define WEIGHT_BLOCK 16.0

// Taps around a pixel on a 3 × 3 grid without the centre. GUIDE_STEP apart they tell a stroke from an
// area: wider than a text stroke (1–2 px at 100 %), narrower than the gap between text and a bar
// next to it (Excel: 4 px from the row numbers to the cell text). A stroke's background is read
// GUIDE_REACH away, beyond the text line: nearer, the 1–2 px gaps of bold text (a sheet tab) voted
// for the letters around them and turned into white specks.
#define GUIDE_STEP 3.0
#define GUIDE_REACH 6.0
#define GUIDE_TAPS 8

// A tap this close in luma keeps the pixel company; with fewer than GUIDE_COMPANY such taps the pixel
// is a stroke (text, a thin line), with more it is part of an area (a corner of a bar has 3).
#define GUIDE_SAME 0.1
#define GUIDE_COMPANY 3
#define GUIDE_SURROUNDED 6

float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

float Similarity(float luma, float background)
{
    float distance = (luma - background) / EDGE_SIGMA;
    return exp(-0.5 * distance * distance);
}

float TapLuma(float2 uv, int k, float reach)
{
    int cell = k < 4 ? k : k + 1;
    float2 offset = float2(cell % 3 - 1, cell / 3 - 1);
    return Luma(Desktop.SampleLevel(PointSampler, uv + offset * reach / TextureSize, 0).rgb);
}

// Smart inversion weight at uv, edge-aware (joint bilateral upsampling): of the four nearest weight
// texels, each counts by its bilinear share AND by how much its background luma matches this pixel. At an
// edge between a light page and a dark bar, a light pixel follows the light texel and a dark one
// the dark texel, so no half-inverted grey seam appears there (plain bilinear gave one, 16–32 px wide).
// A stroke matches by the background around it instead of its own luma: text has the colour of the
// other side (light row numbers on a dark bar next to a white sheet, dark cell text next to that
// bar), so by its own luma it followed the wrong texel – digits vanished, letters stayed dark on black.
float SmartWeight(float2 uv, float pixelLuma)
{
    uint width, height;
    Weights.GetDimensions(width, height);
    float2 size = float2(width, height);

    // Texel i covers desktop pixels [i·BLOCK, (i+1)·BLOCK). Not uv · size: the texture has
    // ceil(width / BLOCK) texels, so that would drift up to a whole texel across the monitor.
    float2 texel = uv * TextureSize / WEIGHT_BLOCK - 0.5;
    int2 base = (int2)floor(texel);
    float2 f = texel - base;

    float2 entries[4];
    float shares[4];
    float lowest = 1;
    float highest = 0;
    [unroll]
    for (int n = 0; n < 4; n++)
    {
        int2 offset = int2(n & 1, n >> 1);
        entries[n] = Weights.Load(int3(clamp(base + offset, int2(0, 0), (int2)size - 1), 0)).rg;
        shares[n] = (offset.x ? f.x : 1 - f.x) * (offset.y ? f.y : 1 - f.y);
        lowest = min(lowest, entries[n].x);
        highest = max(highest, entries[n].x);
    }

    // All four agree (inside a page, a bar or a photo – nearly every pixel): no edge to follow, and
    // no taps read; they doubled the cost of the smart inversion when read everywhere.
    if (highest - lowest < 0.01)
    {
        return entries[0].x;
    }

    int company = 0;
    [unroll]
    for (int k = 0; k < GUIDE_TAPS; k++)
    {
        company += abs(TapLuma(uv, k, GUIDE_STEP) - pixelLuma) < GUIDE_SAME ? 1 : 0;
    }

    // The stroke's background: the far taps unlike it. A stroke also lies on one background – far
    // around it mostly unlike it; a light gap between letters, or between text and a bar, has its
    // own kind beside it (the margin of the cell or tab) and stays an area.
    float far[GUIDE_TAPS];
    float voters = 0;
    [branch]
    if (company < GUIDE_COMPANY)
    {
        [unroll]
        for (int j = 0; j < GUIDE_TAPS; j++)
        {
            far[j] = TapLuma(uv, j, GUIDE_REACH);
            voters += abs(far[j] - pixelLuma) >= GUIDE_SAME ? 1 : 0;
        }
    }

    bool stroke = voters >= GUIDE_SURROUNDED;

    float sum = 0;
    float norm = 0;
    float plain = 0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float2 entry = entries[i];
        float match = Similarity(pixelLuma, entry.y);
        if (stroke)
        {
            float votes = 0;
            [unroll]
            for (int t = 0; t < GUIDE_TAPS; t++)
            {
                votes += abs(far[t] - pixelLuma) >= GUIDE_SAME ? Similarity(far[t], entry.y) : 0;
            }

            match = votes / voters;
        }

        float similar = shares[i] * match;
        sum += similar * entry.x;
        norm += similar;
        plain += shares[i] * entry.x;
    }

    // A pixel unlike all four (e.g. a lone bright dot): the plain bilinear weight.
    return saturate(norm > 1e-4 ? sum / norm : plain);
}

float3 Apply(float4 row0, float4 row1, float4 row2, float3 c)
{
    return saturate(float3(
        dot(row0.xyz, c) + row0.w,
        dot(row1.xyz, c) + row1.w,
        dot(row2.xyz, c) + row2.w));
}

float4 PSMain(VertexOutput input) : SV_Target
{
    float2 inner = (input.Uv - InnerOffset) / InnerScale;
    if (any(inner < 0) || any(inner > 1))
    {
        return FrameColor;
    }

    float2 uv = SourceOffset + inner * SourceScale;
    float3 c = SampleDesktop(uv);

    float4 row0 = ColorRow0;
    float4 row1 = ColorRow1;
    float4 row2 = ColorRow2;
    bool smart = MonitorSmart != 0;
    [loop]
    for (uint i = 0; i < RegionCount; i++)
    {
        float4 rect = RegionRects[i];
        if (all(uv >= rect.xy) && all(uv < rect.zw))
        {
            uint entry = (uint)RegionEntries[i].x;
            row0 = Palette[entry * 3];
            row1 = Palette[entry * 3 + 1];
            row2 = Palette[entry * 3 + 2];
            smart = entry == SmartEntry || (entry == MONITOR_ENTRY && MonitorSmart != 0);
            break;
        }
    }

    float3 filtered = Apply(row0, row1, row2, c);
    if (smart)
    {
        // The rows are the inverted part; light surroundings get it, dark ones keep the original.
        float weight = SmartWeight(uv, Luma(c));
        uint original = ORIGINAL_ENTRY * 3;
        float3 plain = Apply(Palette[original], Palette[original + 1], Palette[original + 2], c);
        filtered = lerp(plain, filtered, weight);
    }

    // The overlay is premultiplied-alpha; the desktop is opaque.
    return float4(filtered, 1);
}
