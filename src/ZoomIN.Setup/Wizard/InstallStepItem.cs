namespace ZoomIN.Setup.Wizard;

/// <summary>One line of the install step list: icon, text and its state for screen readers.</summary>
/// <param name="Glyph">Segoe Fluent icon: pending box, running arrows, tick or cross.</param>
public sealed record InstallStepItem(string Text, string Glyph, string StateText)
{
    public string AutomationName => $"{Text}: {StateText}";
}
