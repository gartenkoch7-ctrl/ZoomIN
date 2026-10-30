namespace ZoomIN.Core.Tracking;

/// <summary>Watches keyboard focus and text caret and publishes them to a <see cref="TrackingModel"/>.</summary>
public interface ITrackingSource : IDisposable
{
    void Start(TrackingModel model);
}
