// Magnified mouse pointer (optionally tinted or inverted) with contrast rim, the pointer
// locator ring, and filled or hollow rectangles (focus frame, caret marker, crosshair).
// Drawn with premultiplied alpha blending on top of the magnified image.

cbuffer CursorConstants : register(b0)
{
    float2 InnerOffset;   // pointer image inside the viewport (the viewport adds room for the rim)
    float2 InnerScale;
    float4 RimColor;      // premultiplied; alpha 0 = no rim
    float2 TexelSize;     // 1 / pointer image size
    float RimTexels;      // rim width in pointer image texels
    float RingThickness;  // ring width relative to its radius (0–1)
    float4 RingColor;     // premultiplied
    float4 Tint;          // rgb = tint colour; w = colour mode: 0 original, 1 tint, 2 inverted
};

Texture2D Pointer : register(t0);
SamplerState BorderSampler : register(s0); // outside the image: transparent

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

VertexOutput VSMain(uint id : SV_VertexID)
{
    VertexOutput output;
    output.Uv = float2((id << 1) & 2, id & 2);
    output.Position = float4(output.Uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return output;
}

// Same rules as PointerPreview.Colored (Core), which draws the settings preview.
float4 Recolor(float4 c)
{
    if (c.a <= 0 || Tint.w < 0.5)
    {
        return c;
    }

    if (Tint.w > 1.5)
    {
        return float4(c.a - c.rgb, c.a);
    }

    float luma = dot(c.rgb, float3(0.2126, 0.7152, 0.0722)) / c.a;
    return float4(Tint.rgb * luma * c.a, c.a);
}

float4 PSCursor(VertexOutput input) : SV_Target
{
    float2 uv = (input.Uv - InnerOffset) / InnerScale;
    float4 color = Recolor(Pointer.Sample(BorderSampler, uv));
    if (RimColor.a == 0)
    {
        return color;
    }

    // Rim = how close opaque pointer pixels are (dilated alpha), drawn behind the pointer.
    float near = 0;
    [unroll] for (int i = 0; i < 8; i++)
    {
        float angle = i * 0.785398;
        float2 offset = float2(cos(angle), sin(angle)) * RimTexels * TexelSize;
        near = max(near, Pointer.Sample(BorderSampler, uv + offset).a);
    }

    return color + RimColor * near * (1 - color.a);
}

// Hollow rectangle (focus frame) or, with thickness ≥ 0.5, a filled one (caret marker).
// TexelSize holds the line thickness relative to the viewport here.
float4 PSRect(VertexOutput input) : SV_Target
{
    float2 edge = min(input.Uv, 1 - input.Uv);
    return (edge.x < TexelSize.x || edge.y < TexelSize.y) ? RingColor : 0;
}

float4 PSRing(VertexOutput input) : SV_Target
{
    float d = length(input.Uv - 0.5) * 2; // 0 in the centre, 1 at the viewport edge
    float width = fwidth(d);
    float inner = 1 - RingThickness;
    float alpha = smoothstep(inner - width, inner, d) * (1 - smoothstep(1 - width, 1, d));
    return RingColor * alpha;
}
