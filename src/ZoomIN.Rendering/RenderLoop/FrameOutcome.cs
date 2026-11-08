namespace ZoomIN.Rendering.RenderLoop;

/// <summary>What one pass of the render loop did (M14 step 3).</summary>
internal enum FrameOutcome
{
    /// <summary>A frame was drawn and presented.</summary>
    Presented,

    /// <summary>The compositor did not ask for a frame in time (zoomin.render.frames_missed).</summary>
    Missed,

    /// <summary>Nothing changed: nothing drawn or presented, the last frame stays on screen.</summary>
    Idle,
}
