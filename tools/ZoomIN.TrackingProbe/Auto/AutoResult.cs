using System.Globalization;
using System.Text;
using ZoomIN.Core.Viewport;
using ZoomIN.TrackingProbe.Interop;

namespace ZoomIN.TrackingProbe.Auto;

/// <summary>Outcome of one application: which source answered before and after the Chromium nudge, and what ZoomIN would use.</summary>
internal sealed class AutoResult
{
    private static readonly string[] s_helpers = ["nvda", "Narrator", "Magnify", "ZoomIN", "jfw", "ZoomText"];

    public AutoResult(AutoScenario scenario)
    {
        Scenario = scenario;
    }

    public AutoScenario Scenario { get; }

    public string? Problem { get; set; }

    public SourceHits Before { get; } = new();

    public SourceHits After { get; } = new();

    public PhysicalRect? Window { get; set; }

    public PhysicalRect? Caret { get; set; }

    public bool Estimated { get; set; }

    public int Readings { get; set; }

    public int Valid { get; set; }

    /// <summary>Caret "moved" reports of the real tracking thread within 3 s without typing; must be 0.</summary>
    public int CaretNews { get; set; }

    public bool Passed => Problem is null && Valid > 0 && Valid == Readings && CaretNews == 0
        && (Scenario.Expect == "estimated" ? Estimated : !Estimated);

    public static string ToMarkdown(IEnumerable<AutoResult> results)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"# Automatischer Tracking-Test {DateTime.Now:yyyy-MM-dd HH:mm}");
        text.AppendLine();
        text.AppendLine(Environment());
        text.AppendLine();
        text.AppendLine("Quellen: S = System-Caret, M = MSAA, U = UIA-Text, F = Fokus, Δ = größte Abweichung zwischen den");
        text.AppendLine("Caret-Quellen in px. „vorher“ = ohne Chromium-Anstoß.");
        text.AppendLine("ZoomIN = Ergebnis der Kette (`FocusReader`); gültig = Caret liegt im App-Fenster und ist 4–200 px hoch.");
        text.AppendLine("Fehlbewegungen = „Caret bewegt“-Meldungen des echten Tracking-Threads in 3 s ohne Tippen (soll 0).");
        text.AppendLine();
        text.AppendLine("| App | Ergebnis | vorher | nachher | ZoomIN-Caret | gültig | Fehlbewegungen | Hinweis |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            var caret = r.Caret is { } c
                ? string.Create(CultureInfo.InvariantCulture, $"{c.X:0},{c.Y:0} {c.Width:0}×{c.Height:0}{(r.Estimated ? " (geschätzt)" : string.Empty)}")
                : "–";
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {r.Scenario.Name} | {(r.Passed ? "✅" : "❌")} | {r.Before} | {r.After} | {caret} | {r.Valid}/{r.Readings} | {r.CaretNews} | {r.Problem ?? (r.Passed ? string.Empty : $"Fokus: {r.After.FocusInfo}")} |");
        }

        return text.ToString();
    }

    // A running screen reader (or another magnifier) switches Chromium's accessibility on by itself,
    // which makes the before/after comparison meaningless – so the report says whether one ran.
    private static string Environment()
    {
        const uint GetScreenReader = 0x0046;
        ProbeNative.SystemParametersInfo(GetScreenReader, 0, out var flag, 0);
        var helpers = s_helpers
            .Where(name => System.Diagnostics.Process.GetProcessesByName(name).Length > 0)
            .ToArray();
        return $"Umgebung: Screenreader-Flag {(flag != 0 ? "**an**" : "aus")}, laufende Hilfsprogramme: {(helpers.Length > 0 ? string.Join(", ", helpers) : "keine")}.";
    }
}
