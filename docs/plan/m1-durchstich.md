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

## Aufgaben

### 1. Overlay-Fenster (`Rendering/Overlay/`)
- [ ] Win32-Fenster: `WS_EX_NOREDIRECTIONBITMAP | WS_EX_LAYERED | WS_EX_TRANSPARENT |
      WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`, randlos über dem Monitor
- [ ] `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` – Rückgabewert prüfen, Fehler loggen
      und Zoom **nicht** anzeigen, wenn der Ausschluss fehlschlägt
- [ ] DirectComposition: Device, Target, Visual; Swapchain per `CreateSwapChainForComposition`
      (premultiplied alpha, Flip-Discard, Waitable, `MaxFrameLatency = 1`)
- [ ] Z-Order über Taskleiste/Startmenü prüfen

### 2. GPU-Grundlagen (`Rendering/Gpu/`)
- [ ] `GpuDevice`: D3D11-Device auf dem Adapter des Monitors, BGRA, optional WARP (für Tests)
- [ ] Shader `Magnify.hlsl`: Fullscreen-Triangle, Quellrechteck bilinear samplen
- [ ] Shader beim Build vorkompilieren (Build-Target in `Rendering.csproj`)

### 3. Capture (`Rendering/Capture/`)
- [ ] `DesktopDuplicator`: `DuplicateOutput`, `AcquireNextFrame(0)`, letzte Textur halten
- [ ] `DXGI_ERROR_ACCESS_LOST` → neu aufbauen (Test: `Win+L`, UAC-Dialog)

### 4. Render-Thread (`Rendering/RenderLoop/`)
- [ ] Eigener Thread, hohe Priorität, MMCSS
- [ ] Pro Frame: Waitable → `GetCursorPos` → Quellrechteck (Faktor 2, Fixpunkt Maus) →
      Capture → Draw → Present
- [ ] Sauberes Starten/Stoppen, alle Ressourcen `IDisposable`

### 5. Minimale Steuerung (`Platform/Input/`)
- [ ] `Strg+Alt+Z` per `RegisterHotKey` schaltet Zoom an/aus
- [ ] Systemzeiger bleibt sichtbar (steht dank Fixpunkt korrekt) – großer Zeiger in M5

### 6. Tests & Messen
- [ ] WARP-Test: `Magnify`-Shader rendert Testbild offscreen, Pixelwerte an Stichproben prüfen
      (neues Testprojekt `tests/ZoomIN.Rendering.Tests`)
- [ ] Debug-Log: Frame-Zeit, verpasste Frames
- [ ] Messung 60 Hz und 144 Hz → [performance.md](../kb/performance.md)
- [ ] Freigabetests → Protokoll in [capture-ausschluss.md](../kb/capture-ausschluss.md)

## Akzeptanzkriterien
- [ ] Bildrate = Monitor-Hz bei 60 und 144 Hz, p99 Frame-Zeit < 1 Frame
- [ ] Klick auf einen Button im Zoom trifft genau diesen Button
- [ ] Teams, Zoom/Webex, Discord, Browser-Freigabe zeigen den unvergrößerten Desktop
- [ ] `Win+L` → nach Entsperren läuft der Zoom weiter
- [ ] WARP-Shadertest läuft lokal und in CI
- [ ] ADR-0006 bestätigt (Status „vorläufig“ entfernen) oder ersetzt

## Offene Fragen / Risiken
- **Hauptrisiko des Projekts.** Scheitert der Capture-Ausschluss mit DComp, Variante B testen;
  scheitern beide → stoppen und neu planen.
- Parallel laufende Windows-Lupe kann stören → beobachten.
- Provisorische Viewport-Mathe wandert in M2 nach Core.
