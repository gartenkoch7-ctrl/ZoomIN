# M8 – Release 1.0

**Ziel:** Eine stabile, gemessene, dokumentierte Version, die man herunterladen und
im Alltag nutzen kann.
**Demo:** GitHub-Release mit ZIP und Installer; frische Windows-VM → installieren →
funktioniert.
**Voraussetzungen:** M7.

## Aufgaben

### 1. Qualität & Performance
- [ ] Messreihe nach `performance.md` auf Hardware-Matrix:
      Onboard-Grafik-Laptop, Desktop mit dedizierter GPU, Hybrid-Laptop, 4K, 144 Hz
- [ ] Dauertest 8 h (Zoom aktiv, Office/Browser): kein Speicherwachstum, keine Ruckler
- [ ] Vollständiger Capture-Ausschluss-Test (Teams, Zoom, Webex, OBS, Snipping Tool)
- [ ] Alle Akzeptanzkriterien M1–M7 erneut prüfen (Regressionsliste)

### 2. Stabilität
- [ ] Globaler Exception-Handler: Log schreiben, Systemzeiger + AppBar wiederherstellen
- [ ] Watchdog: hängt der Render-Thread > 2 s → Overlay ausblenden, neu starten
- [ ] Crash-Logs lokal, **keine** Telemetrie ohne ausdrückliche Zustimmung

### 3. Verteilung
- [ ] Portable ZIP (self-contained, x64 + ARM64)
- [ ] Installer (z. B. Inno Setup oder MSIX) – Entscheidung als ADR-0009
- [ ] Code-Signing beantragen (SignPath Foundation für OSS); bis dahin SmartScreen-Hinweis
      in der Doku erklären
- [ ] GitHub-Actions-Workflow `release.yml`: Tag `v*` → Build, Tests, Artefakte, Release-Entwurf

### 4. Dokumentation
- [ ] `README.md`: Was ist ZoomIN, Screenshots (eigene!), Download, Systemanforderungen
- [ ] Benutzerhandbuch `docs/benutzer/` (Deutsch, Englisch): Hotkeys, Modi, Profile
- [ ] `CONTRIBUTING.md`: Verweis auf AGENTS.md, Clean-Room-Regeln, Setup
- [ ] Bekannte Einschränkungen: Hardware-Capture, Secure Desktop, DRM, ggf. HDR

### 5. Feedback
- [ ] Beta mit 3–5 sehbehinderten Testpersonen, Feedback in Issues
- [ ] Kritische Punkte beheben, Rest in Backlog

## Akzeptanzkriterien
- [ ] Alle Performance-Budgets auf der Hardware-Matrix erfüllt
- [ ] 8-h-Dauertest bestanden
- [ ] Installation auf frischem Windows 10 2004 und Windows 11 erfolgreich
- [ ] Release `v1.0.0` auf GitHub veröffentlicht
- [ ] Roadmap: alle Meilensteine **erledigt**, Post-MVP-Liste gepflegt

## Offene Fragen / Risiken
- Unsignierte EXE: SmartScreen und Virenscanner (Hooks + Overlay wirken verdächtig) →
  früh bei Microsoft zur Analyse einreichen.
- Testpersonen frühzeitig suchen (Blinden- und Sehbehindertenverbände).
