using System.Diagnostics;

namespace ZoomIN.Core.Pointer;

/// <summary>Recognises that the mouse moves again after a rest of at least 3 seconds.</summary>
public sealed class IdleDetector
{
    private const double RestSeconds = 3;

    private long _lastMove;

    /// <returns>True when this movement ends a long enough rest.</returns>
    public bool Add(long timestamp)
    {
        var rested = _lastMove != 0 && (timestamp - _lastMove) / (double)Stopwatch.Frequency >= RestSeconds;
        _lastMove = timestamp;
        return rested;
    }
}
