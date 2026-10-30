namespace ZoomIN.Core.Filters;

/// <summary>How magnified pixels are interpolated (M5 decision: text-optimised by default).</summary>
public enum Smoothing
{
    /// <summary>Sharpening bicubic (Catmull-Rom): edges of text stay crisp at high zoom.</summary>
    Text,

    /// <summary>Bilinear: soft, slightly blurry text from about 4×.</summary>
    Soft,

    /// <summary>Nearest pixel: hard blocks, no interpolation.</summary>
    Pixel,
}
