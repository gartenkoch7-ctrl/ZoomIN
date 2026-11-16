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

    public bool BreakawayAllowed => InJob && (JobLimits & (BreakawayOk | SilentBreakawayOk)) != 0;

    public bool ParentIsEaseOfAccess =>
        ParentProcess is { } name && s_easeOfAccessParents.Any(known => known.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Ease of Access started this instance: its broker is the parent, or the previous instance left
    /// that job for us. "In a job with an exited parent" no longer counts (Q9, 2026-11-16): the
    /// autostart task starts ZoomIN the same way (task job, rundll32 exits at once) and needs the
    /// Win+U signal; a restart after an update looks the same.
    /// </summary>
    public bool ByEaseOfAccess => LeftJob || ParentIsEaseOfAccess;

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
