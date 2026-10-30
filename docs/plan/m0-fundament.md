# M0 – Fundament

**Ziel:** Projektgerüst, das alle Architekturregeln von Anfang an automatisch prüft.
**Demo:** `dotnet build` und `dotnet test` grün, leeres WinUI-3-Fenster startet, CI grün.
**Voraussetzungen:** keine.

## Aufgaben

### 1. Build-Konfiguration
- [x] `global.json` – SDK `10.0.x`, `rollForward: latestFeature`, Test-Runner MTP
- [x] `Directory.Build.props` – `Nullable=enable`, `TreatWarningsAsErrors=true`,
      `ImplicitUsings=enable`, `LangVersion=latest`, `AnalysisLevel=latest-recommended`
- [x] `Directory.Packages.props` – zentrale Paketversionen (Central Package Management)
- [x] `.editorconfig` – file-scoped Namespaces, Namensregeln, Zeilenenden
- [x] `.gitattributes` – `* text=auto eol=crlf`, `*.sh`/`pre-commit` mit `eol=lf`

### 2. Projekte anlegen
| Projekt | Target Framework | Besonderheit |
|---|---|---|
| `src/ZoomIN.Core` | `net10.0` | keine Paketabhängigkeiten |
| `src/ZoomIN.Platform` | `net10.0-windows10.0.19041.0` | CsWin32 (`NativeMethods.txt`) |
| `src/ZoomIN.Rendering` | `net10.0-windows10.0.19041.0` | Vortice.Direct3D11, Vortice.DXGI |
| `src/ZoomIN.App` | `net10.0-windows10.0.19041.0` | WinUI 3, `WindowsPackageType=None`, self-contained |
| `tests/ZoomIN.Core.Tests` | `net10.0` | xunit v3 |
| `tests/ZoomIN.Architecture.Tests` | `net10.0-windows10.0.19041.0` | NetArchTest.Rules |
| `tests/ZoomIN.Benchmarks` | `net10.0` | BenchmarkDotNet |

- [x] `ZoomIN.slnx` mit allen Projekten
- [x] Projekt-Referenzen exakt nach [architektur.md](../kb/architektur.md)
- [x] `app.manifest`: Per-Monitor-DPI v2, `supportedOS` Windows 10

### 3. Architekturtests
- [x] Core hängt von keinem anderen ZoomIN-Projekt und keinem `Windows.*`/`Vortice.*` ab
- [x] Platform und Rendering referenzieren sich nicht gegenseitig
- [x] Keine Namespaces/Ordner `Services`, `Helpers`, `Utils`
- [x] P/Invoke (`DllImport`/`LibraryImport`) nur in `*.Interop`-Namespaces
- [x] Gegenprobe: absichtlich falsche Referenz → Test schlägt fehl (danach entfernt)

### 4. Minimal-App
- [x] WinUI-3-App mit Hauptfenster „ZoomIN“ (zeigt GPU-Status), startet ohne Fehler
- [x] DI-Container in `App/Composition/`
- [x] Logging-Abstraktion in Core (`ILog`), einfache Datei-Implementierung in Platform
      (`%LocalAppData%\ZoomIN\logs\`)

### 5. CI
- [x] Workflow baut und testet die Solution auf `windows-latest`
- [ ] Badge im README (sobald GitHub-Remote und README existieren)

## Akzeptanzkriterien
- [x] Clean Clone → `dotnet build` + `dotnet test` grün ohne manuelle Schritte
- [ ] CI grün (wartet auf GitHub-Remote; lokal mit identischen Befehlen grün)
- [x] Architektur-Gegenprobe erfolgreich
- [x] App startet per `dotnet run --project src/ZoomIN.App`

## Offene Fragen / Risiken
- ✅ WinUI 3 unpackaged + self-contained startet ohne Bootstrapper →
  [build-und-tooling.md](../kb/build-und-tooling.md)
- ✅ `.slnx` funktioniert mit dem .NET 10 SDK.
