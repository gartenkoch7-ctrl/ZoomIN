namespace ZoomIN.Core.Lifecycle;

/// <summary>
/// How the tray app was started (ADR-0030). Ease of Access starts the assistive technologies of its
/// "start after sign-in" list inside a job; with TerminateOnDesktopSwitch=1 that job ends at the next
/// lock screen or UAC prompt, so the tray app leaves it by starting itself again outside the job.
/// </summary>
/// <param name="InJob">The process belongs to a job object.</param>
/// <param name="JobLimits">LimitFlags of the immediate job (JOBOBJECT_BASIC_LIMIT_INFORMATION), 0 outside a job.</param>
/// <param name="ParentProcess">Process name of the parent (no path), or null if it has exited or is unknown.</param>
/// <param name="LeftJob">Started by an instance that left the Ease of Access job (<see cref="LeftJobSwitch"/>).</param>
public sealed record LaunchOrigin(bool InJob, uint JobLimits, string? ParentProcess, bool LeftJob)
{
    /// <summary>Marker of the instance started outside the job; keeps it from relaunching again.</summary>
    public const string LeftJobSwitch = "--left-job";

    /// <summary>JOB_OBJECT_LIMIT_BREAKAWAY_OK: a child may leave with CREATE_BREAKAWAY_FROM_JOB.</summary>
    public const uint BreakawayOk = 0x0000_0800;

    /// <summary>JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK: every child leaves the job by itself.</summary>
    public const uint SilentBreakawayOk = 0x0000_1000;

    // Ease of Access starts ATs through ATBroker.exe; Win+U and the sign-in screen go through Utilman.
    private static readonly string[] s_easeOfAccessParents = ["ATBroker", "utilman"];

    /// <summary>Not started by Ease of Access, or no information yet.</summary>
    public static LaunchOrigin Unknown { get; } = new(false, 0, null, false);

    /// <summary>Started with "--wait-for" after an update or restart: its parent has exited on purpose.</summary>
    public bool AfterRestart { get; init; }

    public bool BreakawayAllowed => InJob && (JobLimits & (BreakawayOk | SilentBreakawayOk)) != 0;

    public bool ParentIsEaseOfAccess =>
        ParentProcess is { } name && s_easeOfAccessParents.Any(known => known.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Ease of Access started this instance: its broker is the parent, or the instance is in a job
    /// whose creator has already exited (the broker need not wait), or the previous instance left
    /// that job for us. A start from the Start menu or a terminal has a living parent that is neither,
    /// and a restart's parent exits on purpose, so neither counts.
    /// </summary>
    public bool ByEaseOfAccess => LeftJob || ParentIsEaseOfAccess || (InJob && ParentProcess is null && !AfterRestart);

    /// <summary>Start outside the job and exit: only once, and only if the job lets a child leave.</summary>
    public bool ShouldLeaveJob => !LeftJob && ByEaseOfAccess && BreakawayAllowed;

    /// <summary>Removes <see cref="LeftJobSwitch"/> so the remaining arguments parse as before.</summary>
    public static string[] ExtractMarker(string[] args, out bool leftJob)
    {
        ArgumentNullException.ThrowIfNull(args);
        leftJob = Array.IndexOf(args, LeftJobSwitch) >= 0;
        return leftJob ? [.. args.Where(arg => arg != LeftJobSwitch)] : args;
    }
}
