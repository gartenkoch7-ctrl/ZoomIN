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
| **Telemetrie (Katalog)** | [docs/kb/telemetrie.md](docs/kb/telemetrie.md) | du ein Feature baust – jedes bringt Logs/Abläufe mit |
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

### 2.5 Telemetrie (ADR-0019)
- **Jedes Feature** bringt seine Telemetrie mit: Logs über `ILogger<T>` mit
  `[LoggerMessage]`-Methoden (eine `*Log.cs` je Feature-Ordner, EventId im Bereich des Projekts),
  Abläufe als `Telemetry.Source.StartActivity("zoomin.<feature>.<vorgang>")`, Messwerte über
  `Telemetry.Meter`. Alles steht im Katalog [docs/kb/telemetrie.md](docs/kb/telemetrie.md).
- **Keine Inhalte des Nutzers** in Logs oder Attributen: kein Fenstertitel, Elementname, Text,
  kein voller Pfad (enthält den Benutzernamen) – nur Prozessname, Fensterklasse,
  Steuerelementtyp, Rechtecke, Dateinamen eigener Dateien, technische Werte.
- Im **Render-Loop** nur Metriken mit beim Start angelegten Instrumenten/Tags; keine Logs,
  keine Activities pro Frame.
- Vor einem harten Prozessende (`Environment.FailFast`) `Telemetry.FlushBeforeExit()` aufrufen.
- Startet ZoomIN einen eigenen Prozess, gibt es den Ablauf mit `TraceParentArgument.Append` weiter.
- Erzwungen durch `TelemetryConventionTests` und den Analyzer CA1848 (nur `[LoggerMessage]`).

### 2.6 Statische Klassen (ADR-0025)
- `static` nur für Code **ohne Nebenwirkungen**: reine Logik, Konstanten, `[LoggerMessage]`-Klassen
  und `Telemetry`, Interop, `Program`, DI-Erweiterungen in `Composition/`, reine Texttabellen.
  Win32-Rückrufe ohne Kontextzeiger (z. B. Low-Level-Hooks) dürfen einen statischen Einsprung
  haben, der nur an eine Instanz weiterleitet.
- Alles mit Dateien, Registry, Prozessen, Dialogen, Netz, Umgebungsvariablen, Pfaden, Uhr oder
  Sprache ist ein **Dienst** (Interface + Implementierung, registriert in der Composition Root).
  Zeit über `TimeProvider`, Pfade über `IAppFolders`, Sprache über `ILocalizer` (`Text` ist nur
  die Fassade für XAML).
- Im Render-Loop Dienste nur als `readonly`-Feld aus dem Konstruktor, nie pro Frame aus dem Container.
- Die IO-Teile (Dateien, Registry, Prozesse, Umgebung, Netz) erzwingt ein Architekturtest. Er
  kommt mit M14 Schritt 6, anfangs mit schrumpfender Ausnahmeliste. Dialoge, Uhr und Sprache
  prüft das Review.

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
./scripts/publish.ps1 -Version 1.0.0-rc1      # Release: artifacts/ (ZIP, Installer mit Inno Setup)
./scripts/check-file-length.ps1               # 300-Zeilen-Regel prüfen
git config core.hooksPath .githooks           # Pre-Commit-Hook aktivieren (einmalig)
```

---

## 8. Definition of Done

- [ ] `dotnet build` ohne Warnungen, `dotnet test` grün
- [ ] `check-file-length.ps1` grün (keine Datei > 300 Zeilen)
- [ ] Schichtregeln eingehalten (Architekturtests grün)
- [ ] Hot Path geändert? → Benchmark mit `0 B` Allokation
- [ ] Telemetrie mitgeliefert (Logs/Abläufe/Messwerte nach 2.5) und im Katalog `docs/kb/telemetrie.md`
- [ ] Neues Wissen in `docs/kb/`, neue Grundsatzentscheidung als ADR
- [ ] Overlay-Fenster aus Bildschirmaufnahmen ausgeschlossen
- [ ] Clean-Room-Regeln eingehalten
- [ ] Code-Review-Schleife nach Abschnitt 9 ohne offene Befunde durchlaufen

---

## 9. Review, Commit, Push, Merge

1. **Vor jedem Commit ein Code-Review** der gesamten Änderung (z. B. `/code-review`).
2. **Führt das Review zu Änderungen**, wird danach wieder der **komplette** geänderte Code
   reviewt – nicht nur die Korrekturen.
3. Schritt 2 wiederholen, **bis ein Review keine Änderungen mehr ergibt**. Erst dann committen
   (Build, Tests und `check-file-length.ps1` vorher grün, siehe Abschnitt 8).
4. **Push** auf den eigenen Feature-Branch ist danach ohne Rückfrage erlaubt.
5. **Mergen** (in `develop`, `main` oder per PR) nur mit ausdrücklicher Genehmigung des Nutzers.

---

## 10. Selbstständig arbeiten

- **Probleme so weit wie möglich selbst lösen:** reproduzieren, Daten beschaffen (Logs, Traces,
  Debug-Schnappschuss, Settings), Ursache finden, beheben, testen, prüfen – ohne den Nutzer für
  Zwischenschritte zu brauchen. Nachfragen nur bei echten Entscheidungen (Produkt, Datenschutz,
  Grundsatzfragen), nicht für Arbeit, die ein Agent selbst erledigen kann.
- **Werkzeuge nutzen:** Dateien des installierten ZoomIN (AppData) über das Terminal-Panel in
  `TestResults/` kopieren; Oberflächen per Computer-Use bedienen, wenn der Nutzer es erlaubt hat.
- **Adminrechte:** Braucht ein Schritt Administratorrechte (Installation, `Program Files`,
  Signatur, Dienste), zeigt der Agent den Schritt mit Begründung an, der Nutzer bestätigt ihn
  (UAC-Abfrage). Nicht darum herumarbeiten und nicht ohne Bestätigung erhöhen.
- **Was ein Mensch sehen muss** (das Overlay ist aus Aufnahmen ausgenommen): erst den
  Debug-Schnappschuss nutzen; nur wenn das nicht reicht, den Nutzer um eine Sichtprüfung bitten.

---

## 11. Parallel arbeiten (Zeit sparen)

Bei **jedem** Plan und vor jeder Umsetzung prüfen: Was davon kann gleichzeitig laufen?

### Im Plan
- Jede Meilensteindatei hat einen Abschnitt **„Parallelisierung“** mit:
  - **Spuren:** je Spur Inhalt, Branch, Start, worauf sie wartet, EventId-Bereich
  - einem kleinen Ablaufbild
  - **Schritt 0:** gemeinsame Bausteine, die mehrere Spuren brauchen. Sie kommen zuerst und
    nacheinander.
  - **Exklusiv-Dateien:** gehören genau einer Spur
  - **Hotspots:** Dateien, die alle Spuren nur ergänzend ändern, etwa Einstellungen, Texte,
    Kürzel, Telemetrie-Katalog
  - **Merge-Reihenfolge**
- Zwei Spuren, die dieselbe Exklusiv-Datei ändern müssen, werden **eine** Spur, die nacheinander
  arbeitet.
- **EventId-Bereiche** werden im Plan je Spur vergeben, damit es keine Doppelbelegung gibt.

### In der Umsetzung
1. **Integrationsbranch** je Meilenstein (`feat/mNN-…` von `develop`). Die steuernde Sitzung
   setzt dort Schritt 0 um, mit Review-Schleife nach §9.
2. **Spuren** laufen als Subagenten mit eigenem Worktree (`isolation: worktree`) oder als eigene
   Sitzungen. Jede Spur:
   - hat einen eigenen Branch vom Integrationsbranch (`feat/mNN-<id>-…`)
   - hält Build, Tests, `check-file-length.ps1` und ihre eigene Review-Schleife (§9) ein
   - committet und pusht nur auf ihren Branch
   - hakt nur ihre eigene Feature-Datei ab
3. **Zusammenführen an einem Strang:** Die steuernde Sitzung mergt die Spuren in der
   Merge-Reihenfolge in den Integrationsbranch. Nach jedem Merge laufen Build und Tests, Konflikte
   in Hotspots löst sie dort.
4. Danach **Gesamt-Review** des ganzen Meilensteins (§9) und Akzeptanzprüfung. Der Merge nach
   `develop` braucht die Freigabe des Nutzers (§9.5).
5. Gleichzeitig laufen nur Spuren mit **getrennten Exklusiv-Dateien**. Im Zweifel nacheinander:
   Ein Merge-Konflikt kostet mehr als die gesparte Zeit.
6. Spuren des **nächsten** Meilensteins dürfen früher starten, wenn ihre Exklusiv-Dateien frei
   sind. Gemergt wird trotzdem erst in dessen Integrationsbranch.
