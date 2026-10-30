# M1 – Technischer Durchstich

**Ziel:** Beweisen, dass der Kern-Ansatz trägt: GPU-Zoom über ein Overlay, flüssig mit
Monitor-Hz, Klicks treffen, **unsichtbar bei Bildschirmfreigabe**. Alles andere ist minimal.
**Demo:** `Strg+Alt+Z` → Hauptmonitor zeigt 2× Zoom um die Maus, flüssig. In Teams, Zoom,
Discord und Browser-Freigabe ist der normale Desktop zu sehen.
**Voraussetzungen:** M0.

## Entscheidungen (Grilling 2026-10-30)
- Overlay direkt mit DirectComposition, kein Vergleichs-Spike → [ADR-0006](../adr/0006-overlay-mit-directcomposition.md)
- Pflicht-Freigabetests: **Microsoft Teams, Zoom/Webex, Discord, Browser-Freigabe (Edge/Chrome)**
- Messen bei **60 Hz und 144 Hz** (Monitor AOC 27G2G4 dafür umstellen)
- Shader werden automatisiert auf **WARP** getestet
- Watchdog erst in M3 ([ADR-0007](../adr/0007-ein-prozess-mit-watchdog.md))
- Shader werden **beim App-Start** kompiliert (nicht beim Build)
- Hauptfenster zeigt Status (Zoom, fps, Ausschluss, Hotkey) und einen Knopf „Zoom an/aus“

## Aufgaben

### 1. Overlay-Fenster (`Rendering/Overlay/`)
- [x] Win32-Fenster: `WS_EX_NOREDIRECTIONBITMAP | WS_EX_LAYERED | WS_EX_TRANSPARENT |
      WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`, randlos, eigener Thread
- [x] `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` – bei Fehler kein Zoom, Fehlermeldung
- [x] DirectComposition + `CreateSwapChainForComposition` (premultiplied, Flip-Discard,
      Waitable, `MaxFrameLatency = 1`)
- [ ] Z-Order über Taskleiste/Startmenü prüfen (manuell)

### 2. GPU-Grundlagen (`Rendering/Gpu/`)
- [x] `GpuDevice`: D3D11 auf dem Adapter des Monitors, BGRA, WARP für Tests
- [x] Shader `Magnify.hlsl`: Fullscreen-Triangle, Quellrechteck bilinear
- [x] Shader beim App-Start kompilieren (Embedded Resource)

### 3. Capture (`Rendering/Capture/`)
- [x] `DesktopDuplicator`: `AcquireNextFrame(0)`, Kopie in eigene Textur, letzte Textur halten
- [x] `DXGI_ERROR_ACCESS_LOST` → neu aufbauen (Code vorhanden)
- [ ] `Win+L` / UAC-Dialog manuell testen

### 4. Render-Thread (`Rendering/RenderLoop/`)
- [x] Eigener Thread, höchste Priorität, MMCSS („Games“)
- [x] Pro Frame: Waitable → Capture → `GetCursorPos` → Quellrechteck (Core) → Draw → Present
- [x] Sauberes Starten/Stoppen, alle Ressourcen `IDisposable`

### 5. Minimale Steuerung
- [x] `Strg+Alt+Z` per `RegisterHotKey` (eigener Thread) schaltet Zoom an/aus
- [x] Systemzeiger bleibt sichtbar – großer Zeiger in M5

### 6. Tests & Messen
- [x] WARP-Tests `tests/ZoomIN.Rendering.Tests` (4 Tests)
- [x] Fixpunkt-Mathe schon in Core (`ViewportCalculator`) mit 12 Tests
- [x] Log: Frames und fps pro Zoom-Sitzung
- [x] Messung 60 Hz: 60,4 fps → [performance.md](../kb/performance.md)
- [ ] Messung 144 Hz (Monitor umstellen)
- [x] Automatischer Aufnahme-Test (GDI): Zoom unsichtbar → [capture-ausschluss.md](../kb/capture-ausschluss.md)
- [ ] Freigabetests Teams, Zoom/Webex, Discord, Browser (manuell, ohne SuperNova)

## Akzeptanzkriterien
- [ ] Bildrate = Monitor-Hz bei 60 ✅ und 144 Hz; p99 Frame-Zeit noch messen
- [ ] Klick im Zoom trifft das sichtbare Element (Mathe per Test bewiesen, manuell bestätigen)
- [ ] Teams, Zoom/Webex, Discord, Browser-Freigabe zeigen den unvergrößerten Desktop
- [ ] `Win+L` → nach Entsperren läuft der Zoom weiter
- [x] WARP-Shadertest läuft lokal (CI wartet auf GitHub-Remote)
- [ ] ADR-0006 bestätigt (nach den Freigabetests)

## Offene Fragen / Risiken
- ✅ Sichttest 2026-10-30 (Nutzer, ohne SuperNova): Zoom funktioniert sauber.
- Allokation pro neuem Desktop-Frame (COM-Wrapper) und 215 ms Startzeit → M2
  (siehe [performance.md](../kb/performance.md)).
- Andere Lupen (SuperNova, Windows-Lupe) beim Testen beenden.
