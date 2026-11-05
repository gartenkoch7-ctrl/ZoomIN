using System.Globalization;
using System.Text;
using ZoomIN.Core.Viewport;

namespace ZoomIN.TrackingProbe;

/// <summary>Counts per application how often each source answered and how far the carets disagree.</summary>
internal sealed class ProbeReport
{
    private readonly Dictionary<string, Tally> _apps = new(StringComparer.OrdinalIgnoreCase);

    public void Add(ProbeSample sample)
    {
        if (!_apps.TryGetValue(sample.Process, out var tally))
        {
            tally = new Tally();
            _apps[sample.Process] = tally;
        }

        tally.Samples++;
        tally.Chromium |= sample.IsChromium;
        tally.Focus += sample.Focus is null ? 0 : 1;
        tally.System += sample.SystemCaret is null ? 0 : 1;
        tally.Uia += sample.UiaCaret is null ? 0 : 1;
        tally.Msaa += sample.MsaaCaret is null ? 0 : 1;
        tally.UiaOffset = Math.Max(tally.UiaOffset, Offset(sample.SystemCaret, sample.UiaCaret));
        tally.MsaaOffset = Math.Max(tally.MsaaOffset, Offset(sample.SystemCaret, sample.MsaaCaret));
    }

    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"# Tracking-Probe {DateTime.Now:yyyy-MM-dd HH:mm}");
        text.AppendLine();
        text.AppendLine("Anteil der Messungen (4×/s), in denen die Quelle etwas lieferte. Abweichung = größter");
        text.AppendLine("Abstand der linken oberen Ecke zum System-Caret in px (nur wenn beide da waren).");
        text.AppendLine();
        text.AppendLine("| App | Chromium | Messungen | Fokus | System-Caret | UIA-Caret | MSAA-Caret | Abw. UIA | Abw. MSAA |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (app, t) in _apps.OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase))
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {app} | {(t.Chromium ? "ja" : "–")} | {t.Samples} | {Share(t.Focus, t.Samples)} | {Share(t.System, t.Samples)} | {Share(t.Uia, t.Samples)} | {Share(t.Msaa, t.Samples)} | {t.UiaOffset:0} | {t.MsaaOffset:0} |");
        }

        return text.ToString();
    }

    private static string Share(int hits, int samples) =>
        string.Create(CultureInfo.InvariantCulture, $"{100.0 * hits / Math.Max(samples, 1):0} %");

    private static double Offset(PhysicalRect? reference, PhysicalRect? other) =>
        reference is { } a && other is { } b ? Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) : 0;

    private sealed class Tally
    {
        public int Samples { get; set; }

        public bool Chromium { get; set; }

        public int Focus { get; set; }

        public int System { get; set; }

        public int Uia { get; set; }

        public int Msaa { get; set; }

        public double UiaOffset { get; set; }

        public double MsaaOffset { get; set; }
    }
}
