namespace ZoomIN.Core.Updates;

/// <summary>Steps of a fresh installation, in order, as the setup shows them (ADR-0014).</summary>
public enum InstallStep
{
    RemoveOld,
    Download,

    /// <summary>The elevated part: Program Files, signature check, Apps entry, sign-in screen.</summary>
    Install,

    /// <summary>The signed-in user's part: autostart, desktop shortcut, settings.</summary>
    Configure,
}
