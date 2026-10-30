# M0 – Fundament

**Ziel:** Projektgerüst, das alle Architekturregeln von Anfang an automatisch prüft.
**Demo:** `dotnet build` und `dotnet test` grün, leeres WinUI-3-Fenster startet, CI grün.
**Voraussetzungen:** keine.

## Aufgaben

### 1. Build-Konfiguration
- [ ] `global.json` – SDK `10.0.x`, `rollForward: latestFeature`
- [ ] `Directory.Build.props` – `Nullable=enable`, `TreatWarningsAsErrors=true`,
      `ImplicitUsings=enable`, `LangVersion=latest`, `AnalysisLevel=latest-recommended`
- [ ] `Directory.Packages.props` – zentrale Paketversionen (Central Package Management)
- [ ] `.editorconfig` – file-scoped Namespaces, Namensregeln, Zeilenenden
- [ ] `.gitattributes` – `* text=auto eol=crlf`, `*.sh`/`pre-commit` mit `eol=lf`

### 2. Projekte anlegen
| Projekt | Target Framework | Besonderheit |
|---|---|---|
| `src/ZoomIN.Core` | `net10.0` | keine Paketabhängigkeiten |
| `src/ZoomIN.Platform` | `net10.0-windows10.0.19041.0` | CsWin32 (`NativeMethods.txt`) |
| `src/ZoomIN.Rendering` | `net10.0-windows10.0.19041.0` | Vortice.Direct3D11, Vortice.DXGI |
| `src/ZoomIN.App` | `net10.0-windows10.0.19041.0` | WinUI 3, `WindowsPackageType=None`, self-contained |
| `tests/ZoomIN.Core.Tests` | `net10.0` | xUnit |
| `tests/ZoomIN.Architecture.Tests` | `net10.0-windows10.0.19041.0` | NetArchTest.Rules |
| `tests/ZoomIN.Benchmarks` | `net10.0` | BenchmarkDotNet |

- [ ] `ZoomIN.slnx` mit allen Projekten
- [ ] Projekt-Referenzen exakt nach [architektur.md](../kb/architektur.md)
- [ ] `app.manifest`: Per-Monitor-DPI v2, `supportedOS` Windows 10

### 3. Architekturtests
- [ ] Core hängt von keinem anderen ZoomIN-Projekt und keinem `Windows.*`/`Vortice.*` ab
- [ ] Platform und Rendering referenzieren sich nicht gegenseitig
- [ ] Keine Namespaces/Ordner `Services`, `Helpers`, `Utils`
- [ ] P/Invoke (`DllImport`/`LibraryImport`) nur in `*.Interop`-Namespaces
- [ ] Gegenprobe: absichtlich falsche Referenz → Test schlägt fehl (danach entfernen)

### 4. Minimal-App
- [ ] WinUI-3-App mit leerem Hauptfenster „ZoomIN“, startet ohne Fehler
- [ ] DI-Container in `App/Composition/` (noch leer)
- [ ] Logging-Abstraktion in Core (`ILog`), einfache Datei-Implementierung in Platform
      (`%LocalAppData%\ZoomIN\logs\`)

### 5. CI
- [ ] Workflow baut und testet die Solution auf `windows-latest`
- [ ] Badge im README

## Akzeptanzkriterien
- [ ] Clean Clone → `dotnet build` + `dotnet test` grün ohne manuelle Schritte
- [ ] CI grün
- [ ] Architektur-Gegenprobe erfolgreich
- [ ] App startet per `dotnet run --project src/ZoomIN.App`

## Offene Fragen / Risiken
- WinUI 3 unpackaged + self-contained: Startverhalten des Windows App SDK Bootstrappers prüfen
  und Ergebnis in `docs/kb/` notieren.
- Prüfen, ob das Format `.slnx` von allen Werkzeugen (CI, IDE) unterstützt wird; sonst `.sln`.
