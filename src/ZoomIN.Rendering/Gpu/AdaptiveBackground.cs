namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// What the adaptive pointer (M15) needs besides its image: the colour pair, the threshold and
/// where the backdrop copy and the desktop lie relative to the render target.
/// </summary>
/// <param name="OnDark">Body colour on dark ground (opaque).</param>
/// <param name="OnLight">Body colour on light ground (opaque).</param>
/// <param name="Threshold">Background brightness 0–1 from which the ground counts as light.</param>
/// <param name="BackdropX">Left edge of the backdrop copy in target pixels.</param>
/// <param name="BackdropY">Top edge of the backdrop copy in target pixels.</param>
/// <param name="BackdropInverseWidth">1 / width of the backdrop texture.</param>
/// <param name="BackdropInverseHeight">1 / height of the backdrop texture.</param>
/// <param name="MonitorTexelU">1 / width of the desktop texture.</param>
/// <param name="MonitorTexelV">1 / height of the desktop texture.</param>
internal readonly record struct AdaptiveBackground(
    FrameColor OnDark,
    FrameColor OnLight,
    float Threshold,
    float BackdropX,
    float BackdropY,
    float BackdropInverseWidth,
    float BackdropInverseHeight,
    float MonitorTexelU,
    float MonitorTexelV);
