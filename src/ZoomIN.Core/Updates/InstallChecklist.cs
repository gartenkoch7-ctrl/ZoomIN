namespace ZoomIN.Core.Updates;

/// <summary>
/// The setup's step list with a tick per finished step. Starting a step marks every earlier step
/// done, so the installer only says what it begins; a failure marks the running step.
/// </summary>
public sealed class InstallChecklist
{
    private readonly InstallStepState[] _states = new InstallStepState[Enum.GetValues<InstallStep>().Length];

    /// <summary>Raised on the caller's thread after every change.</summary>
    public event Action? Changed;

    public static IReadOnlyList<InstallStep> Steps { get; } = Enum.GetValues<InstallStep>();

    public InstallStep? Running => Array.IndexOf(_states, InstallStepState.Running) is var i and >= 0 ? (InstallStep)i : null;

    public InstallStepState StateOf(InstallStep step) => _states[(int)step];

    public void Begin(InstallStep step)
    {
        for (var i = 0; i < (int)step; i++)
        {
            _states[i] = InstallStepState.Done;
        }

        _states[(int)step] = InstallStepState.Running;
        Changed?.Invoke();
    }

    /// <summary>Ends the running step: done on success (and every step before it), failed otherwise.</summary>
    public void Finish(bool succeeded)
    {
        if (Running is not { } running)
        {
            return;
        }

        if (succeeded)
        {
            Array.Fill(_states, InstallStepState.Done, 0, (int)running + 1);
        }
        else
        {
            _states[(int)running] = InstallStepState.Failed;
        }

        Changed?.Invoke();
    }

    /// <summary>Back to all pending, e.g. before "Retry".</summary>
    public void Reset()
    {
        Array.Fill(_states, InstallStepState.Pending);
        Changed?.Invoke();
    }
}
