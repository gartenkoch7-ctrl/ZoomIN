namespace ZoomIN.Core.SecureDesktop;

/// <summary>How much of the engine runs: the tray app runs everything, the secure desktop only the core (M10 Abschluss).</summary>
public enum EngineScope
{
    /// <summary>Tray app: islands, freeze, monitor switch, window drag and everything else.</summary>
    Full,

    /// <summary>Secure desktop (ADR-0016): zoom, filters, tracking and pointer only; nothing that needs a user to arrange it.</summary>
    SecureDesktop,
}
