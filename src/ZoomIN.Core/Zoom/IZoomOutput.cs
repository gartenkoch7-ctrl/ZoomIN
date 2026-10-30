namespace ZoomIN.Core.Zoom;

/// <summary>
/// Shows the magnified desktop according to a <see cref="ZoomModel"/>. Implemented by the
/// Rendering layer. Stays warm while zoom is off so switching on is visible in the next frame.
/// </summary>
public interface IZoomOutput : IDisposable
{
    /// <summary>Snapshot for diagnostics; safe to poll from the UI thread.</summary>
    ZoomStatus Status { get; }

    /// <summary>Prepares GPU resources and starts following the model. Idempotent.</summary>
    void Start();
}
