# M3 – Multi-Monitor & Robustheit

**Ziel:** Zoom auf beliebig vielen Monitoren mit unterschiedlicher DPI/Hz; die App übersteht
alle typischen Systemereignisse ohne Neustart.
**Demo:** Maus wandert vom 4K-Laptop-Display (150 %) auf einen 1080p-Monitor (100 %),
Zoom folgt nahtlos; Monitor ausstecken → läuft weiter.
**Voraussetzungen:** M2.

## Aufgaben

### 1. Monitor-Modell
- [ ] `Core/Monitors/MonitorInfo` (Id, Bounds physisch, DPI, Hz, IsPrimary)
- [ ] `Core/Monitors/IMonitorProvider` + Änderungs-Event
- [ ] `Platform/Monitors/Win32MonitorProvider`: `EnumDisplayMonitors`, `GetDpiForMonitor`,
      Zuordnung `HMONITOR` ↔ DXGI-Output (`DXGI_OUTPUT_DESC.Monitor`)
- [ ] Änderungen erkennen: `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, Geräte-Benachrichtigung

### 2. Pipeline pro Monitor
- [ ] `MonitorPipeline` = Duplicator + Overlay + Swapchain je Monitor
- [ ] `PipelineManager` baut Pipelines bei Änderung auf/ab (außerhalb des Frames)
- [ ] Ein Device pro Adapter; Monitor an anderer GPU → eigenes Device
- [ ] Unterschiedliche Bildraten: ein Render-Thread pro Monitor **oder** einer mit dem
      schnellsten Waitable → im Spike messen, Entscheidung als ADR-0007

### 3. Verhalten über Monitore hinweg
- [ ] **Entscheidung (vorab mit Nutzer):** Was zeigen Monitore ohne Maus?
      Vorschlag: behalten ihren letzten Viewport (wie gewohnt), Option „nur aktiver Monitor zoomt“
- [ ] Übergang am Monitorrand ohne Sprung

### 4. Fehlerfälle (siehe [rendering-pipeline.md](../kb/rendering-pipeline.md))
- [ ] `DXGI_ERROR_DEVICE_REMOVED` → alles neu erstellen (Test: Treiber-Reset per `Win+Strg+Shift+B`)
- [ ] Energiesparmodus/Ruhezustand und Wiederaufnahme
- [ ] Auflösungs-/Ausrichtungswechsel (Hochformat)
- [ ] Monitor hinzufügen/entfernen, Projektionsmodus `Win+P` (Duplizieren/Erweitern)
- [ ] Remote Desktop: Verhalten prüfen und dokumentieren (ggf. sauber deaktivieren)
- [ ] HDR-Monitor: `DuplicateOutput1` mit FP16 nötig? → prüfen, sonst bekannte Einschränkung

### 5. Diagnose
- [ ] Debug-Overlay pro Monitor (Hz, Frame-Zeit, DPI)
- [ ] Jeder Neuaufbau wird geloggt

## Akzeptanzkriterien
- [ ] Getestet mit: 1 Monitor, 2 Monitore gemischte DPI, 2 Monitore gemischte Hz (60 + 144)
- [ ] Alle Fehlerfälle aus 4. überstanden, ohne die App neu zu starten
- [ ] Keine Speicherlecks nach 50× Monitor an/aus (GPU-Speicher stabil)
- [ ] ADR-0007 geschrieben, KB aktualisiert

## Offene Fragen / Risiken
- Hybrid-Grafik (Laptop: Intel + NVIDIA) ist eine häufige Fehlerquelle → früh testen.
- HDR und 10-Bit-Ausgabe können Farbumrechnung erfordern.
