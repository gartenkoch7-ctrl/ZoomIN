# M3 – Multi-Monitor & Robustheit

**Ziel:** Zoom auf beliebig vielen Monitoren mit unterschiedlicher DPI/Hz; die App übersteht
typische Systemereignisse – und ein Watchdog fängt Abstürze auf.
**Demo:** Maus wandert zwischen zwei (virtuellen) Monitoren mit 100 % / 150 % DPI, Zoom folgt
nahtlos; Prozess abschießen → Watchdog startet ZoomIN neu.
**Voraussetzungen:** M2.

## Entscheidungen (Grilling 2026-10-30)
- Nicht aktive Monitore **behalten ihren letzten Ausschnitt**; Option „nur aktiver Monitor zoomt“
- Testen mit **virtuellen Monitoren** (Indirect Display Driver) – nur ein echter Monitor vorhanden
- **Remote Desktop:** versuchen; scheitert Desktop Duplication → klare Meldung, kein Absturz
- **HDR:** bekannte Einschränkung im MVP (Hinweis bei aktivem HDR)
- **Watchdog** wird hier gebaut → [ADR-0007](../adr/0007-ein-prozess-mit-watchdog.md)

## Aufgaben

### 0. Testumgebung zuerst
- [ ] Open-Source-Treiber für virtuelle Monitore installieren, 2. Monitor mit 150 % DPI anlegen
- [ ] **Prüfen:** liefert Desktop Duplication auf dem virtuellen Monitor Frames? Ergebnis in
      [testumgebung.md](../kb/testumgebung.md). Wenn nein → Multi-Monitor nur mit Unit-Tests
      absichern und als „ungetestet“ kennzeichnen

### 1. Monitor-Modell
- [ ] `Core/Monitors/MonitorInfo`, `IMonitorProvider` + Änderungs-Event
- [ ] `Platform/Monitors/Win32MonitorProvider`: `EnumDisplayMonitors`, `GetDpiForMonitor`,
      Zuordnung `HMONITOR` ↔ DXGI-Output
- [ ] Änderungen: `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, Geräte-Benachrichtigung

### 2. Pipeline pro Monitor
- [ ] `MonitorPipeline` (Duplicator + Overlay + Swapchain) je Monitor, `PipelineManager`
- [ ] Ein Device pro Adapter
- [ ] Ein Render-Thread pro Monitor vs. einer für alle → messen, **ADR-0010**

### 3. Verhalten über Monitore
- [ ] `Core`: Viewport-Zustand pro Monitor, letzter Ausschnitt bleibt erhalten
- [ ] Setting „nur aktiver Monitor zoomt“
- [ ] Übergang am Monitorrand ohne Sprung

### 4. Fehlerfälle
- [ ] `DXGI_ERROR_DEVICE_REMOVED` (Test: `Win+Strg+Umschalt+B`)
- [ ] Ruhezustand/Wiederaufnahme, Auflösungs-/Ausrichtungswechsel
- [ ] Monitor an/aus, `Win+P`
- [ ] RDP: Verhalten prüfen, Meldung bei Fehlschlag
- [ ] HDR aktiv → einmaliger Hinweis

### 5. Watchdog (`src/ZoomIN.Watchdog`)
- [ ] Eigenes kleines Projekt, referenziert nur Core; Architekturtest ergänzen
- [ ] Startet mit ZoomIN, überwacht Prozess-Handle; reguläres Beenden wird abgemeldet
- [ ] Absturz → aufräumen (Hooks für M4/M5 vorbereiten), neu starten; max. 3× in 5 min

## Akzeptanzkriterien
- [ ] 1 echter + 1 virtueller Monitor mit gemischter DPI funktionieren (oder dokumentiert,
      warum nicht testbar)
- [ ] Fehlerfälle aus 4. ohne App-Neustart überstanden
- [ ] Prozess per Task-Manager beendet → ZoomIN läuft nach < 3 s wieder
- [ ] Keine Lecks nach 50× Monitor an/aus
- [ ] ADR-0010 geschrieben, KB aktualisiert

## Offene Fragen / Risiken
- Hybrid-Grafik und echte Zweitmonitore sind nicht testbar → Release-Notes „ungetestet“.
