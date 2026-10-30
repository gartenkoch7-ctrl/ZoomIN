namespace ZoomIN.Core.Zoom;

/// <summary>Shows the magnified desktop. Implemented by the Rendering layer.</summary>
public interface IZoomOutput : IDisposable
{
    bool IsRunning { get; }

    /// <summary>Snapshot for diagnostics; safe to poll from the UI thread.</summary>
    ZoomStatus Status { get; }

    void Start(double zoom);

    void Stop();
}
