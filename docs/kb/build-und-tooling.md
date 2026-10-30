# Build & Tooling

Erkenntnisse aus M0 (2026-10-30, .NET SDK 10.0.302).

## Tests: Microsoft.Testing.Platform (MTP)
- xunit v3 läuft auf .NET 10 **nur** mit MTP. Der alte VSTest-Modus bricht mit
  „Testing with VSTest target is no longer supported“ ab.
- Aktiviert über `global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`.
- Aufruf: `dotnet test --solution ZoomIN.slnx` (nicht mehr `dotnet test` ohne Schalter).
- Pakete: nur `xunit.v3` (+ `Microsoft.Testing.Extensions.CodeCoverage` für Abdeckung).
  `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector` werden **nicht**
  gebraucht.
- Testprojekte sind `OutputType=Exe`. Das `using Xunit` wird global über
  `tests/Directory.Build.props` gesetzt – nur für Projekte mit `IsTestProject=true`.
- Name `TestResult` ist mehrdeutig (Xunit vs. NetArchTest) → voll qualifizieren.

## WinUI 3 unpackaged
- Keine `dotnet new`-Vorlage nötig; das Projekt ist von Hand geschrieben und baut mit
  `dotnet build` (kein Visual Studio erforderlich).
- `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`: startet ohne installierte
  Windows-App-SDK-Runtime, kein Bootstrapper-Code nötig.
- `RuntimeIdentifier` fällt auf `$(NETCoreSdkRuntimeIdentifier)` zurück, damit ein
  Solution-Build ohne `-r` funktioniert.

## Ausgabeordner der App
- Solution-Build (`dotnet build ZoomIN.slnx`) baut die App als **x64** nach
  `src/ZoomIN.App/bin/x64/Debug/…/win-x64/`. Ein Projekt-Build ohne Plattform landet in
  `bin/Debug/…` – nicht verwechseln (M2: veralteter Build wurde getestet).
- Welche Version läuft, zeigt der Zeitstempel der `ZoomIN.dll`, nicht der `ZoomIN.exe`
  (der App-Host wird nur bei Änderungen neu kopiert).

## Solution
- `.slnx` funktioniert mit dem .NET 10 SDK (CLI und CI) ohne Probleme.

## Analyzer
- `AnalysisLevel=latest-recommended` + `TreatWarningsAsErrors`.
- Bewusst abgeschaltet (`.editorconfig`): CA1716 (VB-Schlüsselwörter, irrelevant für C#);
  in Tests zusätzlich CA1707 (Unterstriche in Testnamen) und CA1859 (Tests nutzen Interfaces).
- Weitere Abschaltungen nur mit Begründung als Kommentar in `.editorconfig`.

## Architekturtests – Gegenprobe
Absichtliche Verstöße (Rendering → Platform, Namespace `Helpers`) wurden von 3 Tests
erkannt. Bei Änderungen an den Tests die Gegenprobe wiederholen.
