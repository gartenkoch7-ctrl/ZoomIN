# M3 – Multi-Monitor & Robustheit

**Ziel:** Zoom auf beliebig vielen Monitoren mit unterschiedlicher DPI/Hz; die App übersteht
typische Systemereignisse – und ein Watchdog fängt Abstürze auf.
**Demo:** Maus wandert zwischen zwei (virtuellen) Monitoren mit 100 % / 150 % DPI, Zoom folgt
nahtlos; Prozess abschießen → Watchdog startet ZoomIN neu.
**Voraussetzungen:** M2.

## Entscheidungen (Grilling 2026-10-30)
- Nicht aktive Monitore **behalten ihren letzten Ausschnitt**
- **Zoomstufe pro Monitor** (Standard), umschaltbar auf „eine Stufe für alle“
  (`PerMonitorZoom` in `settings.json`; UI-Schalter in M7). Strg+Alt+Z schaltet alle Monitore
  gemeinsam; Mausrad und Bild↑/↓ wirken auf den Monitor unter der Maus
- Testen mit **virtuellem Monitor** (Virtual Display Driver, installiert der Nutzer)
- **Remote Desktop:** versuchen; scheitert Desktop Duplication → klare Meldung, kein Absturz
- **HDR:** bekannte Einschränkung im MVP
- **Watchdog:** dieselbe EXE mit `--watchdog <pid>`; gibt nach 3 Neustarts in 5 min auf und
  zeigt eine **Windows-Benachrichtigung** ([ADR-0007](../adr/0007-ein-prozess-mit-watchdog.md))
- Reihenfolge: **Robustheit zuerst**, dann Multi-Monitor
- Ein Render-Thread + Device pro Monitor → [ADR-0010](../adr/0010-ein-render-thread-pro-monitor.md)

## Aufgaben

### 1. Robustheit
- [x] Anzeigeänderung (`WM_DISPLAYCHANGE`, `WM_DPICHANGED`) → alle Pipelines neu aufbauen
- [x] Auflösungs-/Ausrichtungswechsel bei ACCESS_LOST erkannt → Neuaufbau
- [x] Monitor verschwunden (`DisplayGoneException`) → Neuaufbau
- [x] Sonstige Pipeline-Fehler (z. B. Device Removed) → Neuaufbau nach 2 s, solange gezoomt
- [x] Treiber-Reset (`Win+Strg+Umschalt+B`): Pipeline neu, Zoom sichtbar, kein Prozess-Neustart
- [x] Auflösungswechsel 1920×1080 → 1280×720 → zurück: je ein Neuaufbau, Zoom sichtbar
- [ ] Manuell (Nutzer): `Win+L`, Ruhezustand
- [ ] RDP prüfen, HDR-Hinweis

### 2. Watchdog (`src/ZoomIN.Watchdog`, `Core/Lifecycle/RestartPolicy`)
- [x] Gleiche EXE im Watchdog-Modus, eigener `Program.Main`
- [x] Exit-Code 0 = regulär; sonst Aufräumen (Schritte für M4/M5 vorbereitet) + Neustart
- [x] Max. 3 Neustarts in 5 min (`crashes.log`), dann Windows-Benachrichtigung
- [x] Kein Watchdog, solange ein Debugger hängt
- [x] Architekturtest: Watchdog kennt nur Core

### 3. Multi-Monitor
- [x] `IMonitorProvider` (Core) + `Win32MonitorProvider` (Platform), ID = Gerätename
- [x] `ZoomModel` mit Stufe pro Monitor bzw. geteilt, Snapshot lock-frei
- [x] `MonitorRenderer` pro Monitor, `ZoomEngine` verwaltet und baut neu auf
- [x] Letzter Ausschnitt bleibt auf Monitoren ohne Maus
- [x] Stufen pro Monitor in `settings.json`
- [ ] Test mit virtuellem Zweitmonitor (150 % DPI) – Treiber noch nicht installiert

## Akzeptanzkriterien
- [x] Prozess beendet → ZoomIN läuft nach **~150 ms** wieder; 4. Absturz in 5 min → Hinweis
- [x] Reguläres Schließen → kein Neustart
- [x] Ein-Monitor-Betrieb nach Umbau unverändert (63 Tests grün, Live-Abtastung an/aus)
- [ ] 1 echter + 1 virtueller Monitor mit gemischter DPI funktionieren
- [ ] Fehlerfälle aus 1. ohne App-Neustart überstanden (Treiber-Reset ✅, Auflösung ✅,
      `Win+L`/Ruhezustand offen)
- [ ] Keine Lecks nach 50× Monitor an/aus

## Gefundene Fehler
- **Einstellungen aus M2 brachen den Start (behoben):** Der JSON-Source-Generator setzt bei
  `init`-Eigenschaften fehlende Werte auf `default` statt auf die Code-Standardwerte →
  `MonitorZoomLevels = null` → Absturz in jeder Schleife des Watchdogs. Lösung: `set` statt
  `init`, Regressionstest mit Datei im alten Format. Siehe [build-und-tooling.md](../kb/build-und-tooling.md).

## Offene Fragen / Risiken
- Hybrid-Grafik und echte Zweitmonitore bleiben „ungetestet“ (Release-Notes).
- Ein Absturz direkt beim Start führt zu 3 schnellen Neustarts, dann Aufgabe – gewollt.
- Ohne jede Pipeline (kein Monitor) werden Anzeigeänderungen nicht erkannt.
