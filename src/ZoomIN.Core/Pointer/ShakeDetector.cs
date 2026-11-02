using System.Diagnostics;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Recognises a quick back-and-forth of the mouse: four direction changes of horizontal strokes
/// of at least 40 px within 0.8 s. Small jitter (under 8 px back) does not count as a change.
/// Fed from the mouse hook at up to 1000 samples per second; no allocations.
/// </summary>
public sealed class ShakeDetector
{
    private const int NeededReversals = 4;
    private const double MinStroke = 40;
    private const double Jitter = 8;
    private const double WindowSeconds = 0.8;

    private readonly long[] _reversals = new long[NeededReversals];
    private int _count;
    private int _direction;
    private double _strokeStart;
    private double _extreme;
    private bool _started;

    /// <returns>True once when a shake is recognised.</returns>
    public bool Add(long timestamp, double x)
    {
        if (!_started)
        {
            (_strokeStart, _extreme, _started) = (x, x, true);
            return false;
        }

        // Still moving the same way (or not yet decided): extend the stroke.
        if (_direction == 0 || (x - _extreme) * _direction >= 0)
        {
            if (_direction == 0 && Math.Abs(x - _strokeStart) >= Jitter)
            {
                _direction = Math.Sign(x - _strokeStart);
            }

            _extreme = _direction == 0 ? _strokeStart : x;
            return false;
        }

        if (Math.Abs(x - _extreme) < Jitter)
        {
            return false;
        }

        // Direction change: the stroke that just ended counts if it was long enough.
        var counts = Math.Abs(_extreme - _strokeStart) >= MinStroke;
        (_direction, _strokeStart, _extreme) = (-_direction, _extreme, x);
        if (!counts)
        {
            _count = 0;
            return false;
        }

        _reversals[_count % NeededReversals] = timestamp;
        _count++;
        if (_count < NeededReversals)
        {
            return false;
        }

        var oldest = _reversals[_count % NeededReversals];
        if ((timestamp - oldest) / (double)Stopwatch.Frequency > WindowSeconds)
        {
            return false;
        }

        _count = 0;
        return true;
    }
}
