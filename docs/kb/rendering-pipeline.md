# Rendering-Pipeline

## Überblick
Die gesamte Vergrößerung passiert auf der GPU. Die CPU berechnet pro Frame nur den
Viewport (ein paar Zahlen) und setzt einen Draw-Call ab.

## Schritte pro Monitor
1. **Capture** – `IDXGIOutputDuplication::AcquireNextFrame` liefert den Desktop als
   `ID3D11Texture2D`. Neue Frames nur bei Änderungen; sonst wird die letzte Textur
   wiederverwendet (Timeout 0 im Frame-Loop, nicht blockieren).
2. **Shader-Pass** (ein Fullscreen-Triangle):
   - Sampling des Viewport-Ausschnitts (bilinear, optional bikubisch/Kantenglättung für Text)
   - Farbfilter als 4×4-Farbmatrix + Kontrast/Helligkeit (Konstanten-Buffer)
   - Hervorhebungen (Fokusrahmen, Caret-Marker)
3. **Cursor** – eigener Zeichenpass mit der **neuesten** Mausposition kurz vor `Present`,
   Cursorbild aus `GetCursorInfo`/`GetIconInfo` (gecacht pro Cursor-Handle).
4. **Present** – Flip-Model-Swapchain (`DXGI_SWAP_EFFECT_FLIP_DISCARD`), Waitable Object
   (`DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT`, MaxFrameLatency = 1).

## Frame-Timing
- Render-Thread wartet auf das Waitable Object der Swapchain → läuft exakt mit
  Monitor-Hz, minimale Latenz.
- Zoom-Animation: Zielwert und aktueller Wert in `ZoomState`; pro Frame Easing anhand der
  echten Frame-Zeit (`Stopwatch`), nicht anhand fester Schritte.

## Multi-Monitor
- Ein `DesktopDuplicator` + eine Overlay-Swapchain pro Monitor.
- Alle Monitore an derselben GPU teilen ein `ID3D11Device`. Bei Hybrid-GPU (Laptop):
  Duplication muss auf dem Adapter laufen, an dem der Monitor hängt. ⚠️ unverifiziert, testen.

## Fehlerfälle (müssen behandelt werden)
| Ereignis | Reaktion |
|---|---|
| `DXGI_ERROR_ACCESS_LOST` (Modus-/Auflösungswechsel, UAC, Sperrbildschirm) | Duplication neu erstellen |
| `DXGI_ERROR_DEVICE_REMOVED` (Treiber-Reset) | Device + alle Ressourcen neu erstellen |
| Monitor an/ab, DPI-Wechsel | Monitorliste neu aufbauen, Pipelines anpassen |
| Geschützte Inhalte (DRM-Video) | erscheinen schwarz – erwartet, nicht behebbar |
| Secure Desktop (UAC, Strg+Alt+Entf) | nicht erfassbar ohne Systemdienst – nicht im MVP |

## Shader-Regeln
- HLSL-Dateien in `ZoomIN.Rendering/Shaders/`, beim Build vorkompiliert (`fxc`/`dxc`).
- Max. 300 Zeilen pro Shader-Datei; Gemeinsames in `*.hlsli`.
