namespace ZoomIN.TrackingProbe.Auto;

/// <summary>The applications of the compatibility matrix that are installed on this PC, with their test files.</summary>
internal static class AutoScenarios
{
    public static List<AutoScenario> Create(string work)
    {
        var page = Path.Combine(work, "caret.html");
        File.WriteAllText(page, """
            <!doctype html><meta charset="utf-8"><title>ZoomIN Caret-Test</title>
            <body style="font:20px sans-serif;margin:40px">
            <p>ZoomIN Caret-Test – Textfeld hat den Fokus.</p>
            <textarea id="t" rows="6" cols="50" autofocus>Erste Zeile
            Zweite Zeile mit Text</textarea>
            <script>const t=document.getElementById('t');t.focus();t.setSelectionRange(5,5);</script>
            """);
        var rtf = Path.Combine(work, "caret.rtf");
        File.WriteAllText(rtf, @"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}}\f0\fs28 ZoomIN Caret-Test\par Zweite Zeile\par}");
        var csv = Path.Combine(work, "cells.csv");
        File.WriteAllText(csv, "A;B;C\n1;2;3\n4;5;6\n");

        var url = new Uri(page).AbsoluteUri;
        var browserArgs = $"--no-first-run --no-default-browser-check --disable-sync --app={url}";
        var all = new List<AutoScenario>
        {
            // No Notepad: Windows 11 Notepad restores the user's session (their tabs) on every start,
            // so an automatic run would work inside the user's documents. Test it by hand.
            // The console window is attributed to the shell process, not to conhost.
            new("Konsole (conhost)", "conhost.exe", "cmd.exe /k echo ZoomIN Caret-Test", "cmd", 2, "caret"),
            new("Windows Terminal", "wt.exe", "cmd.exe /k echo ZoomIN Caret-Test", "WindowsTerminal", 4, "caret"),
            new("Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", $"--user-data-dir=\"{Path.Combine(work, "chrome")}\" {browserArgs}", "chrome", 4, "caret"),
            new("Edge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", $"--user-data-dir=\"{Path.Combine(work, "edge")}\" {browserArgs}", "msedge", 4, "caret"),
            new("Word", @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE", $"/q \"{rtf}\"", "WINWORD", 8, "caret"),
            new("Excel", @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE", $"\"{csv}\"", "EXCEL", 8, "estimated"),
        };

        // Only what is installed; notepad/conhost/wt are found via PATH.
        return all.Where(s => !Path.IsPathRooted(s.FileName) || File.Exists(s.FileName)).ToList();
    }
}
