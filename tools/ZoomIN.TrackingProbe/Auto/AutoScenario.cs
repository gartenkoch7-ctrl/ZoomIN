namespace ZoomIN.TrackingProbe.Auto;

/// <summary>
/// One application started by the automatic run. It is opened so that it shows a text caret by
/// itself (editor, document, first cell) – the run never types into it.
/// </summary>
/// <param name="ProcessName">Process that owns the window (the launcher may hand over, e.g. wt → WindowsTerminal).</param>
/// <param name="Expect">What a working ZoomIN should get: "caret" or "estimated" (grid cell instead of caret).</param>
internal sealed record AutoScenario(string Name, string FileName, string Arguments, string ProcessName, double SettleSeconds, string Expect);
