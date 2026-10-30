# M1 – Technischer Durchstich

**Ziel:** Beweisen, dass der Kern-Ansatz trägt: GPU-Zoom über ein Overlay, flüssig mit
Monitor-Hz, Klicks treffen, **unsichtbar in Teams**. Alles andere ist hier bewusst minimal.
**Demo:** Hotkey drücken → Hauptmonitor zeigt 2× Zoom um die Maus, flüssig. In einer
Teams-/OBS-Aufnahme ist der normale Desktop zu sehen.
**Voraussetzungen:** M0.

## Aufgaben

### 1. Spike: Overlay-Fenstervariante (zuerst, max. 2–3 Tage)
Kandidaten bauen und messen, Ergebnis als **ADR-0006** festhalten:
- A) `WS_EX_NOREDIRECTIONBITMAP` + DirectComposition + `CreateSwapChainForComposition`
  (Alpha möglich → später Lupe/Angedockt)
- B) Normales Topmost-Fenster + `CreateSwapChainForHwnd` (Flip-Model)

Prüfen je Variante:
- [ ] Click-through (`WS_EX_TRANSPARENT | WS_EX_LAYERED`) funktioniert
- [ ] `WDA_EXCLUDEFROMCAPTURE` greift (Rückgabewert prüfen!) – Teams, OBS, `Win+Shift+S`
- [ ] Eigene Desktop Duplication sieht das Overlay nicht (keine Rückkopplung)
- [ ] Latenz/Bildrate mit PresentMon
- [ ] Fenster bleibt über Taskleiste/Startmenü (Z-Order)

### 2. GPU-Grundlagen (`ZoomIN.Rendering/Gpu/`)
- [ ] `GpuDevice`: D3D11-Device auf dem Adapter des Hauptmonitors, BGRA-Support
- [ ] `OverlaySwapchain`: Flip-Discard, Waitable Object, `MaxFrameLatency = 1`
- [ ] Shader `Magnify.hlsl`: Fullscreen-Triangle, Sampling eines Quellrechtecks (bilinear)
- [ ] Shader beim Build vorkompilieren (Build-Target in `Rendering.csproj`)

### 3. Capture (`ZoomIN.Rendering/Capture/`)
- [ ] `DesktopDuplicator`: `DuplicateOutput` für den Hauptmonitor, `AcquireNextFrame(0)`
- [ ] Letzte Textur weiterverwenden, wenn kein neuer Frame vorliegt
- [ ] `DXGI_ERROR_ACCESS_LOST` → Duplication neu aufbauen (Test: `Win+L`, UAC-Dialog)

### 4. Render-Thread (`ZoomIN.Rendering/RenderLoop/`)
- [ ] Eigener Thread, hohe Priorität, MMCSS
- [ ] Pro Frame: Waitable abwarten → `GetCursorPos` → Quellrechteck berechnen
      (fester Faktor 2, Fixpunkt Maus) → Capture → Draw → Present
- [ ] Sauberes Starten/Stoppen, alle Ressourcen `IDisposable`

### 5. Minimale Steuerung
- [ ] Globaler Hotkey (Platzhalter `Win+Alt+Z`) schaltet Zoom an/aus
- [ ] Systemzeiger bleibt sichtbar (wegen Fixpunkt steht er korrekt) – vergrößerter Zeiger kommt in M5

### 6. Messen & dokumentieren
- [ ] Debug-Anzeige oder Log: Frame-Zeit, verpasste Frames
- [ ] Messung auf mind. einem 60-Hz- und einem 120/144-Hz-Monitor → `performance.md`
- [ ] Capture-Test → Protokoll in `capture-ausschluss.md`

## Akzeptanzkriterien
- [ ] Bildrate = Monitor-Hz, p99 Frame-Zeit < 1 Frame, keine sichtbaren Ruckler
- [ ] Klick auf einen Button im Zoom trifft genau diesen Button
- [ ] Teams-Freigabe, OBS-Vorschau und Snipping Tool zeigen den unvergrößerten Desktop
- [ ] Sperren/Entsperren (`Win+L`) → Zoom läuft danach weiter
- [ ] ADR-0006 (Overlay-Variante) geschrieben

## Offene Fragen / Risiken
- **Hauptrisiko des Projekts.** Scheitert der Capture-Ausschluss mit Variante A und B, sofort
  stoppen und neu planen (Alternativen in ADR dokumentieren).
- Wenn die eingebaute Windows-Lupe parallel läuft, kann es Konflikte geben → beobachten.
- Rendering-Code, der hier provisorisch ist (Viewport-Mathe), wird in M2 nach Core verschoben.
