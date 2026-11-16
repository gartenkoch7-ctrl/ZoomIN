// Colour replacement of the own schemes (M18, C3), included by Magnify.hlsl after its constant
// buffers. Runs after sampling and before the colour matrix, and before the smart weighting.
// Same formula as ReplacementTable.Apply in ZoomIN.Core (the CPU reference of the tests).

// Rec. 709 YCbCr: luma (Luma from SmartWeight.hlsli, included before) and two colour differences of ±0.5.
float3 ToYcc(float3 c)
{
    float y = Luma(c);
    return float3(y, (c.b - y) / 1.8556, (c.r - y) / 1.5748);
}

// Place + 1 of the own scheme a palette entry shows, 0 for none.
uint SchemeOf(uint entry)
{
    if (entry >= FIRST_SCHEME_ENTRY)
    {
        return entry - FIRST_SCHEME_ENTRY + 1;
    }

    return entry == MONITOR_ENTRY ? MonitorScheme : 0;
}

// Each replacement shifts the colour by (to − from)·w, w = 1 − smoothstep(r/2, r, distance): the
// colour itself becomes exactly the new one, its anti-aliased edges and shades move along.
float3 ReplaceColors(float3 c, uint entry)
{
    uint scheme = SchemeOf(entry);
    if (scheme == 0)
    {
        return c;
    }

    uint first = (scheme - 1) * REPLACEMENTS_PER_SCHEME;
    uint count = ReplaceCounts[scheme - 1].x;
    float3 ycc = ToYcc(c);
    float3 shift = 0;
    [loop]
    for (uint k = 0; k < count; k++)
    {
        float4 from = ReplaceFrom[first + k];
        float weight = 1 - smoothstep(from.w * 0.5, from.w, distance(ycc, from.xyz));
        shift += ReplaceShift[first + k].xyz * weight;
    }

    return saturate(c + shift);
}
