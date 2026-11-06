// Smart inversion (ADR-0024), step 2 of 2, on the texel grid of Ambient.hlsl. Decides what a texel
// alone cannot, from its neighbours and the last frame, then smooths over time:
// 1. Paper (page) by the share of paper samples, with hysteresis: original texels need PAPER_GAIN to
//    become page, page texels stay page down to PAPER_KEEP. A single threshold flickered in video
//    (noise moved burnt-out snow across it every frame).
// 2. Light without paper: crowded text, a frame line or an icon lies on a page – paper in two
//    directions, or paper on one side and a dark area opposite (a thin light strip along a dark
//    window), within PAPER_REACH – and is inverted. Snow in a photo has photo around it and stays.
// 3. Photos and their holes stay original: photo texels; texels without paper between photo on two
//    opposite sides; paper with photo on three sides or an island of paper (burnt-out white).
// 4. Blends with the previous weights while the background stays alike.
// Output: x = inversion weight, y = background luma (as Magnify.hlsl reads it).

cbuffer AmbientConstants : register(b0)
{
    float2 TextureSize;
    float Alpha;          // share of the new weights this frame (1 = no memory)
    uint ProtectPhotos;
    float Block;
    float3 Padding;
};

Texture2D Raw : register(t0);       // Ambient.hlsl: x = paper share (> 0: light), y = paper tone/dark luma, z = photo, w = content luma
Texture2D Previous : register(t1);  // last output of this pass

// Paper share for neighbours, to become page and to stay page (of 16 samples: 6, 8, 4).
#define PAPER_SHARE 0.375
#define PAPER_GAIN 0.5
#define PAPER_KEEP 0.25

// Texels searched in each direction: 2 × 16 px for paper (a text line or an icon), 4 × 16 px for
// photos (the size of the photo test itself).
#define PAPER_REACH 2
#define PHOTO_REACH 4

// Change of background luma from which a texel shows other content (no smoothing), and up to
// which an original texel still shows the same content (for the hysteresis).
#define BACKGROUND_JUMP 0.25
#define SAME_CONTENT 0.1

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

bool IsLight(float4 texel) { return texel.x > 0; }
bool IsPaper(float4 texel) { return texel.x >= PAPER_SHARE; }
bool IsPhoto(float4 texel) { return ProtectPhotos != 0 && texel.z > 0.5; }

// A photo texel towards direction within reach.
bool PhotoTowards(int2 at, int2 direction, int2 size, int reach)
{
    for (int step = 1; step <= reach; step++)
    {
        int2 p = at + direction * step;
        if (any(p < 0) || any(p >= size))
        {
            return false;
        }

        if (IsPhoto(Raw.Load(int3(p, 0))))
        {
            return true;
        }
    }

    return false;
}

int PhotoSides(int2 at, int2 size, out bool opposite)
{
    bool left = PhotoTowards(at, int2(-1, 0), size, PHOTO_REACH);
    bool right = PhotoTowards(at, int2(1, 0), size, PHOTO_REACH);
    bool up = PhotoTowards(at, int2(0, -1), size, PHOTO_REACH);
    bool down = PhotoTowards(at, int2(0, 1), size, PHOTO_REACH);
    opposite = (left && right) || (up && down);
    return (left ? 1 : 0) + (right ? 1 : 0) + (up ? 1 : 0) + (down ? 1 : 0);
}

// Paper inside a photo (photo on three sides; one may be grey rock): photo for its neighbours.
bool InPhoto(int2 at, int2 size)
{
    bool opposite;
    return ProtectPhotos != 0 && PhotoSides(at, size, opposite) >= 3;
}

// Direct neighbours that are light and not photo (page paper is connected, snow islands are not).
int PageNeighbours(int2 at, int2 size)
{
    int count = 0;
    int2 directions[4] = { int2(-1, 0), int2(1, 0), int2(0, -1), int2(0, 1) };
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        int2 p = at + directions[i];
        if (all(p >= 0) && all(p < size))
        {
            float4 texel = Raw.Load(int3(p, 0));
            count += IsLight(texel) && !IsPhoto(texel) ? 1 : 0;
        }
    }

    return count;
}

// What lies towards direction, skipping light texels without paper: 1 = paper, 2 = photo, 3 = dark,
// 0 = nothing within reach (more light without paper, or the edge).
int Beyond(int2 at, int2 direction, int2 size, out float luma)
{
    luma = 0;
    for (int step = 1; step <= PAPER_REACH; step++)
    {
        int2 p = at + direction * step;
        if (any(p < 0) || any(p >= size))
        {
            return 0;
        }

        float4 texel = Raw.Load(int3(p, 0));
        luma = texel.y;
        if (IsPhoto(texel))
        {
            return 2;
        }

        if (IsPaper(texel))
        {
            return InPhoto(p, size) ? 2 : 1;
        }

        if (!IsLight(texel))
        {
            return 3;
        }
    }

    return 0;
}

// Paper in two directions, or (unless strict) paper on one side and a dark area opposite; luma =
// the paper found.
bool OnPage(int2 at, int2 size, bool strict, out float luma)
{
    float l, r, u, d;
    int left = Beyond(at, int2(-1, 0), size, l);
    int right = Beyond(at, int2(1, 0), size, r);
    int up = Beyond(at, int2(0, -1), size, u);
    int down = Beyond(at, int2(0, 1), size, d);
    luma = left == 1 ? l : right == 1 ? r : up == 1 ? u : d;
    int paperSides = (left == 1 ? 1 : 0) + (right == 1 ? 1 : 0) + (up == 1 ? 1 : 0) + (down == 1 ? 1 : 0);
    return paperSides >= 2
        || (!strict && ((left == 1 && right == 3) || (right == 1 && left == 3) || (up == 1 && down == 3) || (down == 1 && up == 3)));
}

float2 PSMain(VertexOutput input) : SV_Target
{
    int2 at = int2(input.Position.xy);
    uint width, height;
    Raw.GetDimensions(width, height);
    int2 size = int2(width, height);

    float4 raw = Raw.Load(int3(at, 0));
    bool memory = Alpha < 1;
    float2 previous = memory ? Previous.Load(int3(at, 0)).rg : float2(0, 0);

    // Hysteresis only with a last frame (after a pause the old texture is stale, maybe NaN): what
    // was original needs clear evidence to become page, what was page stays page longer. Without it
    // video noise flipped borderline spots (burnt-out snow) every frame – a grey, flickering patch.
    // Only while the same content lies there (its luma as remembered): after scrolling the new
    // content decides freely, or a page could keep a white spot from the content before.
    bool wasOriginal = memory && previous.x < 0.5 && abs(raw.w - previous.y) < SAME_CONTENT;
    bool wasPage = memory && previous.x > 0.5 && abs(raw.y - previous.y) < SAME_CONTENT;
    float threshold = wasOriginal ? PAPER_GAIN : wasPage ? PAPER_KEEP : PAPER_SHARE;
    bool paper = IsLight(raw) && raw.x >= threshold;

    float2 now;
    float luma;
    bool opposite;
    if (IsPhoto(raw))
    {
        now = float2(0, raw.w);
    }
    else if (paper)
    {
        // Paper with photo on three sides, or an island of paper: burnt-out white inside a photo.
        bool inPhoto = ProtectPhotos != 0 && (InPhoto(at, size) || PageNeighbours(at, size) < (wasOriginal ? 3 : 2));
        now = float2(inPhoto ? 0 : 1, raw.y);
    }
    else if (IsLight(raw) && OnPage(at, size, wasOriginal, luma))
    {
        now = float2(1, luma);
    }
    else
    {
        // Light without page around it (photo content, also between photo texels), or dark.
        now = float2(0, raw.w);
    }

    if (!memory)
    {
        return now;
    }

    // Smoothed only while the background stays alike (a borderline spot that would flicker). When
    // it changes clearly – content moved in, e.g. while scrolling – the new decision counts at once;
    // smoothing then let dark and light areas drag grey ghosts behind them (scroll test: 7 % of pixels).
    return abs(now.y - previous.y) > BACKGROUND_JUMP ? now : lerp(previous, now, Alpha);
}
