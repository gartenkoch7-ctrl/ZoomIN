# AGENTS.md – ZoomIN

> **ZoomIN** ist eine kostenlose Open-Source-Bildschirmlupe für Windows (MIT-Lizenz),
> funktional inspiriert von *SuperNova* (Dolphin Computer Access), per **Clean-Room**
> eigenständig entwickelt. Ziel: der flüssigste Zoom, den Windows kann – und für
> Bildschirmübertragungen (Teams, Zoom, OBS) unsichtbar.

Diese Datei ist die **einzige Quelle der Wahrheit** für alle Agents (Claude Code, Codex,
Cursor, …) und Menschen. `CLAUDE.md` importiert sie nur.

---

## 1. Knowledgebase – vor jeder Aufgabe lesen

Lade nur die Dateien, die deine Aufgabe betreffen. Index: [docs/kb/README.md](docs/kb/README.md)

| Thema | Datei | Lesen, wenn … |
|---|---|---|
| Produkt, Scope, MVP | [docs/kb/produkt-und-scope.md](docs/kb/produkt-und-scope.md) | du Features planst |
| Glossar | [docs/kb/glossar.md](docs/kb/glossar.md) | ein Begriff unklar ist |
| Architektur & Schichten | [docs/kb/architektur.md](docs/kb/architektur.md) | du Code anlegst/verschiebst |
| Rendering-Pipeline | [docs/kb/rendering-pipeline.md](docs/kb/rendering-pipeline.md) | du an Capture/GPU/Render-Loop arbeitest |
| Unsichtbar bei Übertragung | [docs/kb/capture-ausschluss.md](docs/kb/capture-ausschluss.md) | du Overlay-Fenster anfasst |
| Eingabe & Tracking | [docs/kb/eingabe-und-tracking.md](docs/kb/eingabe-und-tracking.md) | du Maus/Fokus/Caret/Hotkeys bearbeitest |
| Performance-Budget | [docs/kb/performance.md](docs/kb/performance.md) | du im Hot Path arbeitest |
| SuperNova-Analyse | [docs/kb/supernova-analyse.md](docs/kb/supernova-analyse.md) | du ein Verhalten nachbaust |
| Testumgebung | [docs/kb/testumgebung.md](docs/kb/testumgebung.md) | du testest oder misst |
| Build & Tooling | [docs/kb/build-und-tooling.md](docs/kb/build-und-tooling.md) | Build oder Tests nicht laufen |
| Architekturentscheidungen | [docs/adr/](docs/adr/) | du eine Entscheidung infrage stellst |
| **Roadmap & Meilensteinpläne** | [docs/plan/README.md](docs/plan/README.md) | du eine Aufgabe beginnst – arbeite im aktuellen Meilenstein, hake erledigte Punkte ab |

**Pflege:** Wer etwas Neues lernt (API-Eigenheit, Messwert, Verhaltensbeobachtung),
schreibt es in die passende KB-Datei. Neue Grundsatzentscheidungen → neues ADR
(`docs/adr/NNNN-titel.md`). Bestehende ADRs werden nicht umgeschrieben, sondern ersetzt
(`Status: Ersetzt durch NNNN`).

---

## 2. Harte Architekturregeln (nicht verhandelbar)

### 2.1 Dateigröße
- **Max. 300 Zeilen pro Datei** für Code und Tests (`*.cs`, `*.xaml`, `*.hlsl`, `*.hlsli`, `*.ps1`).
- Ausgenommen: generierte Dateien (`*.g.cs`, `*.Designer.cs`, `bin/`, `obj/`), Lockfiles.
- Geprüft durch `scripts/check-file-length.ps1` – lokal im Pre-Commit-Hook und in CI.
- Nähert sich eine Datei 250 Zeilen: **jetzt** aufteilen, nicht später. Aufteilen nach
  Verantwortung (eigene Klasse), nicht mechanisch (`partial` nur für XAML-Code-Behind).
- Ein öffentlicher Typ pro Datei, Dateiname = Typname.

### 2.2 Schichten & Abhängigkeiten
```
ZoomIN.App ──► ZoomIN.Rendering ──► ZoomIN.Core
     │                                  ▲
     └────────► ZoomIN.Platform ────────┘
```
- **Core** kennt niemanden: keine Win32-, D3D-, WinUI- oder IO-Abhängigkeiten. Reine Logik
  (Viewport-Mathe, Zoom-Zustand, Tracking-Entscheidungen, Settings-Modelle).
- **Platform** und **Rendering** referenzieren nur Core und implementieren dessen Interfaces.
  Sie kennen sich **nicht** gegenseitig.
- **App** ist Composition Root (DI), UI und Tray. Keine Logik, die in Core gehört.
- Erzwungen durch `tests/ZoomIN.Architecture.Tests` (NetArchTest).
- Innerhalb eines Projekts: Ordner nach **Feature** (`Modes/Lens/`, `Filters/`, `Tracking/`),
  nicht nach Typart (`Services/`, `Helpers/` sind verboten).

### 2.3 Render-Hot-Path
- Render-Loop läuft auf **eigenem Thread**, nie auf dem UI-Thread.
- **Keine Heap-Allokationen** im Render-Loop (keine `new`, LINQ, Closures, `string`-Bau,
  Boxing, `params`). Puffer werden beim Start/Resize angelegt und wiederverwendet.
- Kommunikation UI → Render nur über einen unveränderlichen Zustands-Snapshot
  (atomarer Referenztausch), keine Locks im Frame.
- Jede Änderung am Hot Path braucht einen Benchmark-Lauf (siehe `docs/kb/performance.md`).

### 2.4 Plattform-Pflichten
- Jedes Fenster, das vergrößerte Inhalte zeigt, setzt
  `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` – **ohne Ausnahme**.
- Die Windows-Magnification-API (`MagSetFullscreenTransform`, Magnifier-Control) wird
  **nicht** für die Anzeige genutzt (nicht aus Aufnahmen ausschließbar, siehe ADR-0002).
- Per-Monitor-DPI-Awareness v2; jede Koordinate trägt ihren Raum im Typ
  (`PhysicalPoint` vs. `LogicalPoint`), nie nackte `int x, int y`.
- Multi-Monitor ist Pflicht: Logik immer pro Monitor, nie „der Bildschirm“.
- Mindestversion: Windows 10 2004 (Build 19041).

---

## 3. Clean-Room-Regeln (rechtlich verbindlich)

- **Erlaubt:** SuperNova benutzen und Verhalten beobachten, öffentliche Handbücher, Hilfe,
  Webseiten und Videos auswerten, eigene Messungen.
- **Verboten:** Dekompilieren, Disassemblieren, Debuggen oder Hooken von SuperNova-Binaries;
  Code, Grafiken, Icons, Sounds, Texte oder Farbschemata-Dateien von Dolphin übernehmen;
  den Namen „SuperNova“ oder Dolphin-Marken in Produkt, UI oder Paketnamen verwenden.
- Beobachtungen werden in eigenen Worten in `docs/kb/supernova-analyse.md` beschrieben,
  mit Quelle (URL oder „eigene Beobachtung, Version X“). Keine Screenshots von SuperNova im Repo.
- Implementiert wird **nur** aus der KB-Beschreibung, nicht „mit SuperNova daneben“.

---

## 4. Tech-Stack

| Bereich | Wahl |
|---|---|
| Sprache/Runtime | C# 13, **.NET 10 (LTS)** |
| UI | WinUI 3 (Windows App SDK), Fluent Design, Hell/Dunkel |
| GPU | Direct3D 11 + DXGI über **Vortice.Windows** |
| Capture | DXGI Desktop Duplication |
| Overlay | Win32-Fenster (layered, click-through, topmost) mit Flip-Model-Swapchain |
| Fokus/Caret | UI Automation (COM) |
| Settings | JSON in `%AppData%\ZoomIN\` (`System.Text.Json`, Source Generator) |
| Tests | xUnit, NetArchTest, BenchmarkDotNet |
| Lizenz | MIT |

Neue NuGet-Pakete nur mit kurzer Begründung im PR; keine Pakete mit Copyleft-Lizenz.

---

## 5. Code-Konventionen

- Code, Bezeichner, Kommentare, Commit-Messages: **Englisch**. Doku/KB: **Deutsch**.
- `Nullable` enabled, `TreatWarningsAsErrors` true, file-scoped Namespaces.
- Kommentare erklären das **Warum** (z. B. Win32-Eigenheiten), nicht das Was.
- P/Invoke ausschließlich in `ZoomIN.Platform/Interop/` bzw. `ZoomIN.Rendering/Interop/`,
  bevorzugt per `LibraryImport`-Source-Generator oder CsWin32.
- Jede native Ressource ist `IDisposable` und wird deterministisch freigegeben.
- Commits: Conventional Commits (`feat:`, `fix:`, `perf:`, `docs:`, `refactor:`, `test:`).

---

## 6. Tests

- **Core:** Unit-Tests für jede Logik (Viewport-Mathe, Tracking, Zoom-Easing) – Ziel ≥ 90 %.
- **Architektur:** Schichtregeln aus 2.2 als Tests.
- **Performance:** BenchmarkDotNet für Hot-Path-Code; Allokationen müssen `0 B` sein.
- Platform/Rendering: dünn halten, Logik nach Core ziehen, damit sie testbar ist.
- Testdateien unterliegen ebenfalls der 300-Zeilen-Regel.

---

## 7. Befehle

```powershell
dotnet build ZoomIN.slnx                      # Build
dotnet test --solution ZoomIN.slnx            # alle Tests (Microsoft.Testing.Platform, siehe global.json)
dotnet run --project src/ZoomIN.App           # App starten
dotnet run -c Release --project tests/ZoomIN.Benchmarks   # Benchmarks
./scripts/check-file-length.ps1               # 300-Zeilen-Regel prüfen
git config core.hooksPath .githooks           # Pre-Commit-Hook aktivieren (einmalig)
```

---

## 8. Definition of Done

- [ ] `dotnet build` ohne Warnungen, `dotnet test` grün
- [ ] `check-file-length.ps1` grün (keine Datei > 300 Zeilen)
- [ ] Schichtregeln eingehalten (Architekturtests grün)
- [ ] Hot Path geändert? → Benchmark mit `0 B` Allokation
- [ ] Neues Wissen in `docs/kb/`, neue Grundsatzentscheidung als ADR
- [ ] Overlay-Fenster aus Bildschirmaufnahmen ausgeschlossen
- [ ] Clean-Room-Regeln eingehalten
