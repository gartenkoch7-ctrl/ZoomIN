# M8 – Release 1.0

**Ziel:** Eine stabile, gemessene, dokumentierte Version zum Herunterladen.
**Demo:** GitHub-Release mit ZIP und Installer; frische Windows-VM → installieren → funktioniert.
**Voraussetzungen:** M7.

## Entscheidungen (Grilling 2026-10-30)
- **ZIP + Inno-Setup-Installer**, **nur x64** → [ADR-0009](../adr/0009-verteilung-zip-und-inno-setup.md)
- **Keine Beta.** Release-Notes kennzeichnen ehrlich **getestet / ungetestet**;
  Issue-Vorlage für Hardware-Berichte
- Testumgebung: ein PC (RTX 3060 Ti, 1 Monitor 60/144 Hz, Windows 11), virtuelle Monitore,
  Windows-10-VM → [testumgebung.md](../kb/testumgebung.md)

## Aufgaben

### 1. Qualität & Performance
- [ ] Messreihe nach `performance.md` bei 60 und 144 Hz, mit virtuellem Zweitmonitor
- [ ] Dauertest 8 h (Zoom aktiv, Office/Browser): kein Speicherwachstum, keine Ruckler
- [ ] Freigabetest: Teams, Zoom/Webex, Discord, Browser-Freigabe
- [ ] Windows-10-VM (VirtualBox): Start, Zoom, Einstellungen – Grundfunktion
- [ ] Regressionsliste: alle Akzeptanzkriterien M1–M7 erneut prüfen

### 2. Stabilität
- [ ] Globaler Exception-Handler: Log, Zeiger + AppBar wiederherstellen
- [ ] Render-Thread hängt > 2 s → Overlay ausblenden, Pipeline neu starten
- [ ] Watchdog-Szenarien erneut testen
- [ ] Keine Telemetrie; Crash-Logs nur lokal

### 3. Verteilung
- [ ] Portable ZIP (self-contained, win-x64)
- [ ] Inno-Setup-Skript: Installation, Startmenü, optional Autostart, saubere Deinstallation
- [ ] `release.yml`: Tag `v*` → Build, Tests, ZIP + Installer, Release-Entwurf
- [ ] SmartScreen-Hinweis in der Doku; EXE bei Microsoft zur Analyse einreichen

### 4. Dokumentation
- [ ] `README.md`: Was ist ZoomIN, eigene Screenshots, Download, Systemanforderungen
- [ ] Benutzerhandbuch `docs/benutzer/` (DE, EN): Tastenkürzel, Modi, Profile
- [ ] `CONTRIBUTING.md`: Verweis auf AGENTS.md, Clean-Room-Regeln, Setup
- [ ] Release-Notes mit Tabelle **getestet / ungetestet** (Hybrid-Grafik, echte Zweitmonitore,
      Laptop-DPI, HDR, ARM64 nicht unterstützt)
- [ ] `.github/ISSUE_TEMPLATE/hardware-bericht.yml`

## Akzeptanzkriterien
- [ ] Performance-Budgets bei 60 und 144 Hz erfüllt
- [ ] 8-h-Dauertest bestanden
- [ ] Installation auf frischem Windows 11 und Windows-10-VM erfolgreich
- [ ] Release `v1.0.0` auf GitHub veröffentlicht
- [ ] Roadmap: alle Meilensteine **erledigt**

## Offene Fragen / Risiken
- Unsignierte EXE + Hooks + Overlay → Virenscanner-Fehlalarme möglich.
- Ohne Beta tauchen Hardware-Probleme erst nach Release auf → schnelle Patch-Releases einplanen.
