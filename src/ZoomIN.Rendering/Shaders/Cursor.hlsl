// Magnified mouse pointer with contrast rim, and the pointer locator ring.
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

float4 PSCursor(VertexOutput input) : SV_Target
{
    float2 uv = (input.Uv - InnerOffset) / InnerScale;
    float4 color = Pointer.Sample(BorderSampler, uv);
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

float4 PSRing(VertexOutput input) : SV_Target
{
    float d = length(input.Uv - 0.5) * 2; // 0 in the centre, 1 at the viewport edge
    float width = fwidth(d);
    float inner = 1 - RingThickness;
    float alpha = smoothstep(inner - width, inner, d) * (1 - smoothstep(1 - width, 1, d));
    return RingColor * alpha;
}
