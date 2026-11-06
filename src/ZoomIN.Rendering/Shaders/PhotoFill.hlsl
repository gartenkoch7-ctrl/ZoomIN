// Smart inversion (ADR-0024), step 2 of 2, on the texel grid of Ambient.hlsl. Decides what a texel
// alone cannot, from its neighbours, then smooths over time:
// 1. Light without paper (Ambient.hlsl w = 1): crowded text, a frame line or an icon lies on a
//    page: on one side paper, on the opposite side paper or a dark area (a thin light strip along a
//    dark window), within PAPER_REACH – then it is page and is inverted. Snow or clouds in a photo
//    have photo around them (at the photo edge: paper on one side, photo on the other) and stay.
// 2. Holes in photos: a photo is found by its colours, so a white or grey patch in it is no photo
//    on its own. A texel with photo texels on two opposite sides within PHOTO_REACH is protected.
//    The page next to a photo has it on one side only and stays inverted.
// 3. Blends with the previous weights over time, so a spot that changes fades instead of jumping.
// Output: x = inversion weight, y = background luma (as Magnify.hlsl reads it).

cbuffer AmbientConstants : register(b0)
{
    float2 TextureSize;
    float Alpha;          // share of the new weights this frame (1 = no memory)
    uint ProtectPhotos;
    float Block;
    float3 Padding;
};

Texture2D Raw : register(t0);       // Ambient.hlsl: x = paper, y = background luma, z = photo, w = light without paper
Texture2D Previous : register(t1);  // last output of this pass

// Texels searched in each direction: 2 × 16 px for paper (a text line or an icon), 4 × 16 px for
// photos (the size of the photo test itself).
#define PAPER_REACH 2
#define PHOTO_REACH 4

// Change of background luma from which a texel shows other content (no smoothing).
#define BACKGROUND_JUMP 0.25

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

// The first texel towards direction (within reach) whose channel is set; found = false if none.
float4 Towards(int2 at, int2 direction, int2 size, int reach, int channel, out bool found)
{
    found = false;
    for (int step = 1; step <= reach; step++)
    {
        int2 p = at + direction * step;
        if (any(p < 0) || any(p >= size))
        {
            break;
        }

        float4 texel = Raw.Load(int3(p, 0));
        if (texel[channel] > 0.5)
        {
            found = true;
            return texel;
        }
    }

    return 0;
}

// Photo texels in at least three of the four directions within PHOTO_REACH: paper there is a
// burnt-out white area inside a photo (sky, snow), not page. Three, not four: one side may be grey
// rock. The page along a coloured bar or between two icons has photo on two sides at most.
bool Surrounded(int2 at, int2 size)
{
    bool left, right, up, down;
    Towards(at, int2(-1, 0), size, PHOTO_REACH, 2, left);
    Towards(at, int2(1, 0), size, PHOTO_REACH, 2, right);
    Towards(at, int2(0, -1), size, PHOTO_REACH, 2, up);
    Towards(at, int2(0, 1), size, PHOTO_REACH, 2, down);
    return (left ? 1 : 0) + (right ? 1 : 0) + (up ? 1 : 0) + (down ? 1 : 0) >= 3;
}

// What lies towards direction, skipping light texels without paper: 1 = paper, 2 = photo, 3 = dark,
// 0 = nothing within reach (more light without paper, or the edge).
int Beyond(int2 at, int2 direction, int2 size, int reach, out float luma)
{
    luma = 0;
    for (int step = 1; step <= reach; step++)
    {
        int2 p = at + direction * step;
        if (any(p < 0) || any(p >= size))
        {
            return 0;
        }

        float4 texel = Raw.Load(int3(p, 0));
        luma = texel.y;
        if (texel.z > 0.5)
        {
            return 2;
        }

        if (texel.x > 0.5)
        {
            // Paper inside a photo is photo for its neighbours, not page.
            return ProtectPhotos != 0 && Surrounded(p, size) ? 2 : 1;
        }

        if (texel.w < 0.5)
        {
            return 3;
        }
    }

    return 0;
}

// Paper on one side and paper or dark on the opposite side; luma = the paper found.
bool OnPage(int2 at, int2 size, out float luma)
{
    float l, r, u, d;
    int left = Beyond(at, int2(-1, 0), size, PAPER_REACH, l);
    int right = Beyond(at, int2(1, 0), size, PAPER_REACH, r);
    int up = Beyond(at, int2(0, -1), size, PAPER_REACH, u);
    int down = Beyond(at, int2(0, 1), size, PAPER_REACH, d);
    luma = left == 1 ? l : right == 1 ? r : up == 1 ? u : d;
    return (left == 1 && (right == 1 || right == 3)) || (right == 1 && left == 3)
        || (up == 1 && (down == 1 || down == 3)) || (down == 1 && up == 3);
}

// Set texels on two opposite sides; luma = mean background luma of the pair found.
bool Enclosed(int2 at, int2 size, int reach, int channel, out float luma)
{
    bool left, right, up, down;
    float4 l = Towards(at, int2(-1, 0), size, reach, channel, left);
    float4 r = Towards(at, int2(1, 0), size, reach, channel, right);
    float4 u = Towards(at, int2(0, -1), size, reach, channel, up);
    float4 d = Towards(at, int2(0, 1), size, reach, channel, down);
    luma = left && right ? (l.y + r.y) * 0.5 : (u.y + d.y) * 0.5;
    return (left && right) || (up && down);
}

float2 PSMain(VertexOutput input) : SV_Target
{
    int2 at = int2(input.Position.xy);
    uint width, height;
    Raw.GetDimensions(width, height);
    int2 size = int2(width, height);

    float4 raw = Raw.Load(int3(at, 0));
    float2 now = float2(raw.x, raw.y);
    float luma;
    bool photo = ProtectPhotos != 0 && raw.z > 0.5;
    if (photo)
    {
        // A photo itself (Ambient.hlsl z, only set with protection on) stays original.
        now.x = 0;
    }
    else if (raw.w > 0.5 && OnPage(at, size, luma))
    {
        // On a page: inverted, matched against the paper around it.
        now = float2(1, luma);
    }
    else if (ProtectPhotos != 0 && raw.x < 0.5 && Enclosed(at, size, PHOTO_REACH, 2, luma))
    {
        // Texels without paper: snow or clouds in a photo never have it (measured), while the
        // page between two coloured icons or along a coloured bar does and stays inverted.
        now.x = 0;
    }
    else if (ProtectPhotos != 0 && raw.x > 0.5 && Surrounded(at, size))
    {
        // Paper with photo on all four sides: a burnt-out white area (sky, snow) inside a photo.
        now.x = 0;
    }

    // After a pause the old texture is stale or never written (may even hold NaN): not read at all.
    if (Alpha >= 1)
    {
        return now;
    }

    // Smoothed only while the background stays alike (a borderline spot that would flicker). When
    // it changes clearly – content moved in, e.g. while scrolling – the new decision counts at once;
    // smoothing then let dark and light areas drag grey ghosts behind them (scroll test: 7 % of pixels).
    float2 previous = Previous.Load(int3(at, 0)).rg;
    return abs(now.y - previous.y) > BACKGROUND_JUMP ? now : lerp(previous, now, Alpha);
}
