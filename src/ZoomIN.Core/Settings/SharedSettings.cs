using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Updates;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Settings;

/// <summary>
/// The application part of <see cref="AppSettings"/> shared by all profiles: language, updates,
/// diagnostics, KI-Debug, filter per window, own colour schemes and frozen monitors. A base record
/// only to keep AppSettings within the file-size rule, like <see cref="InputSettings"/>: the
/// properties are inherited, so the settings file keeps them at its top level as before (M19).
/// </summary>
/// <remarks>Properties use <c>set</c>, not <c>init</c>, for the same reason as in <see cref="AppSettings"/>.</remarks>
public abstract record SharedSettings : InputSettings
{
    // --- Application (shared by all profiles) ---
    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>False until the first-run assistant was completed (M7).</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>The "closes to tray" hint is shown only once.</summary>
    public bool CloseToTrayHintShown { get; set; }

    /// <summary>The migration to the margin mode (M17 A1) still has to tell the user once where to change it back.</summary>
    public bool MouseFollowHintPending { get; set; }

    /// <summary>Update check at start and once a day (ADR-0014); only asks GitHub for the release list.</summary>
    public bool CheckForUpdates { get; set; }

    /// <summary>Which releases are offered (ADR-0014): finished ones only, or also experimental builds.</summary>
    public ReleaseChannel UpdateChannel { get; set; } = ReleaseChannel.Stable;

    public string ActiveProfile { get; set; } = ProfileFields.DefaultProfileName;

    // --- Diagnostics (shared by all profiles; ADR-0019, read through AppSettingsTelemetry) ---
    public TelemetryLevel TelemetryLevel { get; set; } = TelemetryOptions.Default.Level;

    public bool TelemetryLogs { get; set; } = TelemetryOptions.Default.Logs;

    public bool TelemetryTraces { get; set; } = TelemetryOptions.Default.Traces;

    public bool TelemetryMetrics { get; set; } = TelemetryOptions.Default.Metrics;

    public LogLevel TelemetryLogLevel { get; set; } = TelemetryOptions.Default.LogLevel;

    public int TelemetryRetentionDays { get; set; } = TelemetryOptions.Default.RetentionDays;

    public DateTimeOffset? TelemetryRaisedUntil { get; set; }

    public string TelemetryOtlpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// KI-Debug (ADR-0027): an agent may read the state, take debug snapshots and trigger shortcuts
    /// over a pipe. Off from the factory; stays on until the user turns it off (user's decision).
    /// </summary>
    public bool AgentDebugEnabled { get; set; }

    /// <summary>Filter per window (ADR-0020): on/off, automatic filter, sensitivity and the user's rules per app + window class.</summary>
    public WindowFilterOptions WindowFilter { get; set; } = WindowFilterOptions.Default;

    /// <summary>
    /// Own colour schemes (M18, C1–C3), at most 4; the definitions are shared by all profiles, the
    /// choice (<see cref="ColorFilter"/>, rules) belongs to the profile. None from the factory.
    /// </summary>
    public IReadOnlyList<CustomScheme> CustomSchemes { get; set; } = [];

    // --- Frozen monitors (G2, M19): nothing frozen is ever saved ---

    /// <summary>What a frozen monitor shows: live (factory) or the still image of the moment of freezing.</summary>
    public FrozenContent FrozenContent { get; set; } = FrozenContent.Live;

    /// <summary>Colour of the border of a frozen monitor, "#RRGGBB".</summary>
    public string FrozenFrameColor { get; set; } = RgbColor.Cyan.ToHex();
}
