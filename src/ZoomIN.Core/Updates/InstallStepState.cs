namespace ZoomIN.Core.Updates;

/// <summary>State of one <see cref="InstallStep"/> in the <see cref="InstallChecklist"/>.</summary>
public enum InstallStepState
{
    Pending,
    Running,
    Done,
    Failed,
}
