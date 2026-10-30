using ZoomIN.Core.Modes;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// Last zoom level per mode – shared, and per monitor (M3/M4 decisions). Not thread-safe;
/// <see cref="ZoomModel"/> uses it under its lock.
/// </summary>
public sealed class ZoomLevels
{
    private readonly Dictionary<ZoomMode, double> _shared = [];
    private readonly Dictionary<ZoomMode, Dictionary<string, double>> _perMonitor = [];

    /// <summary>Start levels: fullscreen 2×, lens and docked 3× (stronger magnification is typical there).</summary>
    public static IReadOnlyDictionary<ZoomMode, double> Defaults { get; } = new Dictionary<ZoomMode, double>
    {
        [ZoomMode.Fullscreen] = ZoomLimits.DefaultLevel,
        [ZoomMode.Lens] = 3.0,
        [ZoomMode.Docked] = 3.0,
    };

    public double Get(ZoomMode mode, string monitorId, bool perMonitor) =>
        perMonitor && _perMonitor.TryGetValue(mode, out var levels) && levels.TryGetValue(monitorId, out var level)
            ? level
            : Shared(mode);

    public void Set(ZoomMode mode, string monitorId, double level, bool perMonitor)
    {
        level = Valid(level, mode);
        if (perMonitor)
        {
            ForMode(mode)[monitorId] = level;
        }
        else
        {
            _shared[mode] = level;
        }
    }

    public double Shared(ZoomMode mode) => _shared.TryGetValue(mode, out var level) ? level : Defaults[mode];

    public void SetShared(ZoomMode mode, double level) => _shared[mode] = Valid(level, mode);

    /// <summary>Copy for persisting.</summary>
    public IReadOnlyDictionary<string, double> PerMonitor(ZoomMode mode) =>
        _perMonitor.TryGetValue(mode, out var levels) ? new Dictionary<string, double>(levels) : new Dictionary<string, double>();

    public void Load(ZoomMode mode, double shared, IReadOnlyDictionary<string, double>? perMonitor)
    {
        SetShared(mode, shared);
        foreach (var (id, level) in perMonitor ?? new Dictionary<string, double>())
        {
            ForMode(mode)[id] = Valid(level, mode);
        }
    }

    private Dictionary<string, double> ForMode(ZoomMode mode)
    {
        if (!_perMonitor.TryGetValue(mode, out var levels))
        {
            levels = [];
            _perMonitor[mode] = levels;
        }

        return levels;
    }

    private static double Valid(double level, ZoomMode mode) =>
        level > ZoomLimits.Min ? ZoomLimits.Clamp(level) : Defaults[mode];
}
