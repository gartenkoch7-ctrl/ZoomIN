namespace ZoomIN.Core.SecureDesktop;

/// <summary>A process the secure instance looks after (its watchdog).</summary>
/// <param name="Alive">The process is still running.</param>
/// <param name="InJob">It runs in a job object; null if that could not be asked.</param>
public readonly record struct ProbedProcess(bool Alive, bool? InJob);
