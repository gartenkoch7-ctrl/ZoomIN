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

## JSON-Einstellungen: kein `init`
Der `System.Text.Json`-Source-Generator weist **fehlenden `init`-Eigenschaften `default(T)` zu**
– Standardwerte aus dem Code gehen verloren, wenn eine ältere Datei eine neue Eigenschaft nicht
enthält (M3: `null`-Dictionary → Absturz beim Start). Deshalb haben `AppSettings`-Eigenschaften
`set`; Regressionstest `Load_OlderFileWithoutNewProperties_KeepsTheirDefaults`.

## UI-Tests per UI Automation (M7)
Windows PowerShell 5.1 kann `System.Windows.Automation` laden und damit das
Einstellungsfenster wie ein Screenreader bedienen (Navigation per `SelectionItemPattern`,
Namensprüfung). Skripte mit Umlauten **mit BOM speichern** – sonst liest PowerShell 5.1 sie
als ANSI („Tastenkürzel“ → „TastenkÃ¼rzel“). Screenshots von WinUI-Fenstern:
`PrintWindow(hwnd, dc, 2)` (PW_RENDERFULLCONTENT); Mica erscheint dabei schwarz.

## WinUI 3 ohne Fenster
`Application.DispatcherShutdownMode = OnExplicitShutdown`, sonst endet die App mit dem
letzten Fenster. Eigener `Program.Main` mit `DISABLE_XAML_GENERATED_MAIN`.

## Watchdog beim Testen
Skripte, die ZoomIN per `Stop-Process` beenden, gelten für den Watchdog als Absturz: Er startet
neu und schreibt `%LocalAppData%\ZoomIN\crashes.log`. Nach Tests die Datei löschen, sonst
blockieren echte Abstürze schneller.

## UIAccess-Build lokal installieren (ADR-0012)
Nur dieser Build liegt über Schnelleinstellungen, Startmenü und Info-Center
(capture-ausschluss.md, „Z-Ebenen“). Inno Setup ist dafür nicht nötig:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup-signing.ps1     # einmalig, als Administrator
./scripts/publish.ps1 -UiAccess                                         # normaler Benutzer, signiert
powershell -ExecutionPolicy Bypass -File scripts/install-uiaccess.ps1  # als Administrator
```
- `install-uiaccess.ps1` kopiert nach `C:\Program Files\ZoomIN`, legt einen Startmenü-Eintrag an
  und startet ZoomIN über Explorer, also **nicht** erhöht. `-Remove` deinstalliert.
- Danach nicht parallel `dotnet run` testen: ZoomIN läuft nur einmal (`SingleInstance`).
- Execution Policy blockt unsignierte Skripte → `-ExecutionPolicy Bypass` bzw. PowerShell-Tool.

## Analyzer
- `AnalysisLevel=latest-recommended` + `TreatWarningsAsErrors`.
- Bewusst abgeschaltet (`.editorconfig`): CA1716 (VB-Schlüsselwörter, irrelevant für C#);
  in Tests zusätzlich CA1707 (Unterstriche in Testnamen) und CA1859 (Tests nutzen Interfaces).
- Weitere Abschaltungen nur mit Begründung als Kommentar in `.editorconfig`.

## Architekturtests – Gegenprobe
Absichtliche Verstöße (Rendering → Platform, Namespace `Helpers`) wurden von 3 Tests
erkannt. Bei Änderungen an den Tests die Gegenprobe wiederholen.
