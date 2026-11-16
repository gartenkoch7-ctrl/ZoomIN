using System.Diagnostics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Records ZoomIN's operations while the OpenTelemetry tracer is still being built in the
/// background (start-up budget, ADR-0019). Only .NET's own listener, so it costs almost nothing.
/// Operations that end before the tracer exists are kept here and written once it is there;
/// those still running then are finished by the tracer itself.
/// </summary>
internal sealed class EarlyActivities : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _finished = [];
    private readonly Func<bool> _tracesOn;
    private bool _handedOver;

    public EarlyActivities(Func<bool> tracesOn)
    {
        _tracesOn = tracesOn;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                _tracesOn() ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
            ActivityStopped = Keep,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Stops recording and returns what ended so far; later ends belong to the tracer.</summary>
    public Activity[] HandOver()
    {
        lock (_finished)
        {
            _handedOver = true;
            var finished = _finished.ToArray();
            _finished.Clear();
            return finished;
        }
    }

    public void Dispose() => _listener.Dispose();

    private void Keep(Activity activity)
    {
        lock (_finished)
        {
            if (!_handedOver)
            {
                _finished.Add(activity);
            }
        }
    }
}
