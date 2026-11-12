// Edge-aware weight of the smart inversion (ADR-0024), included by Magnify.hlsl after its
// resources: needs Desktop, Weights, PointSampler, TextureSize and TexUv.

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
    return Luma(Desktop.SampleLevel(PointSampler, TexUv(uv + offset * reach / TextureSize), 0).rgb);
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
