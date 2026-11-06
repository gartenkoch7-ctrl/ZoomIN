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

## WinUI-Sprachordner (2026-11-05)
- WinUI bringt die Texte seiner eigenen Steuerelemente (Kontextmenüs, Namen für Bildschirmleser)
  als `Microsoft.ui.xaml.dll.mui` in **86 Sprachordnern** (`af-ZA` … `zh-TW`) mit, ~2,6 MB.
  `SatelliteResourceLanguages=de;en` filtert nur .NET-Ressourcen (`*.resources.dll`), nicht diese.
- Self-contained fügt das Windows App SDK sie als `None`-Elemente mit `Link="fr-FR\…"` hinzu
  (Target `AddMicrosoftWindowsAppSDKPayloadFilesFromComponents`). `Directory.Build.targets`
  entfernt danach alle außer `de-*` und `en-*` → bleiben `de-DE`, `en-GB`, `en-us` (App und Setup,
  in `bin/` und im veröffentlichten Paket).
- In **einen** Sprachordner verschieben geht nicht: Windows lädt eine `.mui` nur aus einem
  Kulturordner direkt neben ihrer DLL.
- ZoomINs eigene Texte sind C# (`App/Localization/Texts*.cs`, `Setup/Localization`), keine `.resw`.
  Kommt eine dritte Sprache dazu, hier das Präfix ergänzen.

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
- `install-uiaccess.ps1` kopiert nach `C:\Program Files\ZoomIN\app-<version>`, setzt den Link
  `current` (ADR-0015), legt einen Startmenü-Eintrag an und startet ZoomIN über Explorer, also
  **nicht** erhöht. `-Remove` deinstalliert. Eine alte flache Installation wird ersetzt.
  `-NoStart` lässt ZoomIN beendet, der Nutzer startet es übers Startmenü. Das ist Pflicht, wenn ein
  Agent installiert: Von dort gestartete Prozesse erben dessen Sandbox und hängen beim Start.
- Danach nicht parallel `dotnet run` testen: ZoomIN läuft nur einmal (`SingleInstance`).
- Eine laufende UIAccess-Instanz lässt sich als normaler Benutzer **nicht** per `Stop-Process`
  beenden („Zugriff verweigert“) – über das Tray beenden oder mit Admin-Rechten.
- Update-Schritt von Hand (wie die App ihn startet), mit Admin-Abfrage:
  `"C:\Program Files\ZoomIN\current\ZoomIN.exe" --apply-update "<paket.zip>" <sha256> <version>`;
  Exit-Code = `UpdateResult` (0 = installiert, 7 = Abfrage abgelehnt).
  Aus einem Skript mit `Start-Process … -Verb RunAs` starten: ohne Erhöhung kein eigener
  Admin-Dialog, sondern Exit-Code 6 (`UnauthorizedAccessException` auf `Program Files`).
  Die laufende alte Instanz beendet der Schritt nicht; danach neu starten (über Explorer).
- Execution Policy blockt unsignierte Skripte → `-ExecutionPolicy Bypass` bzw. PowerShell-Tool.

## Release-Assets und Versionen (ADR-0014)
- `./scripts/package-release.ps1 -Version <v>` macht aus dem **signierten** UIAccess-Ordner
  `ZoomIN-<v>-win-x64-uiaccess.zip` und schreibt `checksums.txt` (SHA-256 aller ZIP/EXE in
  `artifacts/`, sha256sum-Format, ohne BOM, LF). Unsigniert → Abbruch. Die Namen müssen zu
  `ReleaseAssets` passen, sonst ignoriert der Release-Feed den Release. Liegt `ZoomIN-Setup.exe` in
  `artifacts/`, muss es ebenfalls gültig signiert sein; lokal (ohne SignPath) `-AllowUnsignedSetup`.
- Lokal liegt in `artifacts/` evtl. noch ein altes ZIP – es landet mit in `checksums.txt`. Schadet
  nicht, in CI ist der Ordner leer.
- Basisversion steht in `VERSION` (z. B. `1.0.0`). develop-Builds heißen `<VERSION>-dev.<N>`
  (N = Anzahl Commits) und sortieren damit **vor** `1.0.0-rc1` und `1.0.0`. Nach einem Release
  `VERSION` hochsetzen, sonst wären neue develop-Builds „älter“ als der Release.
- Workflows: `build-release.yml` (gemeinsam) ← `release.yml` (Tags) und `develop.yml` (Pushes).
  Ohne Secret `SIGNPATH_API_TOKEN` laufen Build und Tests, veröffentlicht wird nichts.

## Analyzer
- `AnalysisLevel=latest-recommended` + `TreatWarningsAsErrors`.
- Bewusst abgeschaltet (`.editorconfig`): CA1716 (VB-Schlüsselwörter, irrelevant für C#);
  in Tests zusätzlich CA1707 (Unterstriche in Testnamen) und CA1859 (Tests nutzen Interfaces).
- Weitere Abschaltungen nur mit Begründung als Kommentar in `.editorconfig`.

## Architekturtests – Gegenprobe
Absichtliche Verstöße (Rendering → Platform, Namespace `Helpers`) wurden von 3 Tests
erkannt. Bei Änderungen an den Tests die Gegenprobe wiederholen.
