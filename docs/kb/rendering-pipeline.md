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

## Multi-Monitor (ADR-0010)
- Pro Monitor ein `MonitorRenderer`: eigener Thread, eigenes Device auf dem Adapter des
  Monitors, eigene Duplication, eigenes Overlay, eigene Swapchain.
- Monitor-ID = Gerätename `\\.\DISPLAYn` (DXGI `OutputDescription.DeviceName` = GDI `szDevice`).
- Anzeigeänderung → `DisplayChangedException` → `ZoomEngine` baut nach 500 ms alle Renderer neu.
- Monitore ohne Maus behalten ihren letzten Ausschnitt.
- Farbfilter pro Monitor: Jeder Renderer liest `EffectsModel.CurrentFor(Gerätename)`. Für
  Monitore mit eigenem Filter liegt beim Ändern der Einstellungen schon eine fertige
  `DisplayEffects`-Kopie samt Farbmatrix bereit (`EffectsSnapshot`); pro Frame bleibt nur ein
  Dictionary-Lookup (0 B, Benchmark `EffectsRead`).
- Hybrid-GPU: Duplication auf dem Adapter des Monitors. ⚠️ unverifiziert (keine Hardware).

## Fehlerfälle (müssen behandelt werden)
| Ereignis | Reaktion |
|---|---|
| `DXGI_ERROR_ACCESS_LOST` (Modus-/Auflösungswechsel, UAC, Sperrbildschirm) | Duplication neu erstellen |
| `DXGI_ERROR_DEVICE_REMOVED` (Treiber-Reset) | Device + alle Ressourcen neu erstellen |
| Monitor an/ab, DPI-Wechsel | Monitorliste neu aufbauen, Pipelines anpassen |
| Geschützte Inhalte (DRM-Video) | erscheinen schwarz – erwartet, nicht behebbar |
| Secure Desktop (UAC, Strg+Alt+Entf) | für die Benutzerinstanz nicht erfassbar; dort läuft eine eigene Instanz als SYSTEM (`--secure-desktop`, ADR-0016, [sichere-desktops.md](sichere-desktops.md)) |

## Filter, Glättung, Zeiger (M5)
- `Magnify.hlsl`: Abtastung (Catmull-Rom/bilinear/Punkt) → Farbmatrix (3×3 + Offset) → saturate.
  Alle Filter sind eine Matrix, daher gleiche Kosten für jeden Filter.
- `Cursor.hlsl`: zweiter Durchgang mit vormultipliziertem Alpha-Blending (Zeiger, Suchring);
  danach wird das Blending wieder abgeschaltet.
- Bei Zoom 1× mit aktivem Filter rendert das Overlay weiter (Vollbild-Geometrie).

## Filter pro Fenster (M12, ADR-0020/0021)
- **Tracker-Thread** (`Platform/WindowFilters/Win32WindowFilterSource`): WinEvents (Vordergrund,
  Verschieben, Minimieren, Anzeigen/Verbergen/Z-Reihenfolge, Ort, Cloaking) → `WindowTracker.Update`
  listet die sichtbaren Top-Level-Fenster (`EnumWindows` = Z-Reihenfolge) mit
  `DWMWA_EXTENDED_FRAME_BOUNDS` (ohne unsichtbaren Rand), überspringt verborgene, minimierte,
  ge-cloakte und klick-durchlässige Overlays (`WS_EX_TRANSPARENT|WS_EX_LAYERED`, auch ZoomINs
  eigene) und Schatten (`SysShadow`). Name/Klasse je Fenster nur einmal lesen.
- **Einstufung** einmal beim ersten Erscheinen: GDI-`StretchBlt(COLORONCOLOR)` auf 64×256 auf dem eigenen
  Blick-Thread `LookWorker` (ADR-0022; Overlays sind aus der Kopie ausgenommen); `BrightnessMeter` zählt nur sichtbare
  Pixel (nicht verdeckt, auf einem Monitor), `BrightnessVote` entscheidet 2 von 3. Verborgen und
  wieder gezeigt = keine neue Messung (Zustand lebt bis `IsWindow` false).
- **Titelleiste (ADR-0023):** Höhe aus `DWMWA_CAPTION_BUTTON_BOUNDS` (auch Titelleisten im
  Client-Bereich, z. B. Einstellungen/Explorer), sonst Client-Oberkante. Ein Blick kopiert das Fenster
  einmal in 64×256 (ein zweiter `StretchBlt` = zweiter DWM-Frame, 33 statt 17 ms); Zeilen der Titelleiste
  werden für sich gezählt, nur die Fläche darunter entscheidet hell/dunkel.
  Dunkle Titelleiste über eigenem Filter → eigener Bereich mit Monitorfilter vor dem Fensterbereich.
  Von Hand je App: Automatisch · Wie das Fenster · Nie filtern.
- **Snapshot** `WindowRegions` (≤ 64 Rechtecke + Palettenindex) per `WindowFilterModel.Publish`;
  gleicher Inhalt → kein Wecken. Render-Thread: `ChangesMonitor` (65 ns bei 64 Bereichen, 0 B) entscheidet über
  Pause; `WindowConstants.Fill` + Upload in `b1` nur bei neuem Snapshot oder neuen Effekten.
- **Shader:** Quell-UV gegen die Rechtecke (Monitor-UV), erstes Treffer-Rechteck wählt die Matrix
  aus `Palette` (9 Einträge), sonst Monitor-Matrix. Gilt auch beim Zoom, weil im Quellraum geprüft.
- **Gemessen (Dev-PC, 2026-11-06):** Aktualisieren 0,10 ms bei 16 sichtbaren Fenstern; ein Blick
  16,5 ms (1200×800) bzw. 17 ms (ganzer Monitor) – GDI wartet auf den nächsten DWM-Frame; ein
  sichtbares Fenster ist nach ≈ 0,6 s entschieden (Blicke bei 250 und 600 ms).
- **GDI-Blicke blockieren:** jeder `StretchBlt` vom Bildschirm-DC wartet auf den nächsten DWM-Frame
  (≈ 17 ms, egal wie groß, egal ob HALFTONE oder COLORONCOLOR). Auf dem Tracker-Thread verzögerte
  das WinEvents um bis zu 75 ms (Menüs blitzten) – deshalb eigener Blick-Thread (ADR-0022).
  Danach: Popup sichtbar → Bereich Median 3,3 ms, 90 % < 4,5 ms.
- **Lupe/Angedockt mit Filter:** zuerst ein Vollbild-Durchgang in 1× (Monitor- und Fensterfilter),
  darüber die Lupe; ohne Filter bleibt außen alles durchsichtig (ADR-0022 Nr. 4).
- **Nicht automatisch prüfbar:** Versatz beim Ziehen (Ereignis vs. Duplication-Frame) und die
  sichtbare Wirkung – das Overlay ist aus Screenshots ausgenommen; Sichtprüfung durch Menschen.

## Intelligentes Invertieren (M13, ADR-0024)
- **Zwei Durchgänge:** `Ambient.hlsl` (`AmbientPass`) schreibt je 16 × 16 Desktop-Pixel ein Gewicht
  0–1 in eine `R16_FLOAT`-Textur (1440p → 160 × 90). `Magnify.hlsl` liest sie bilinear in
  **Desktop-UV** und mischt zwischen „Original“ (Palette 1, nur Kontrast/Helligkeit) und
  „Invertiert, Farbton erhalten“. In Desktop-UV gemessen, damit beim Zoomen nichts umkippt.
- **Entscheidung je Texel:** 4 × 4 Proben im Abstand von 8 px, jede genau zwischen vier Pixeln
  (bilinear = ihr Mittel) → Umfeld 32 × 32 px, Nachbar-Texel überlappen. Gewicht =
  `smoothstep(0,45; 0,75; Anteil heller Proben)`, eine Probe zählt hell ab Luma 0,55–0,75. Text auf
  Weiß behält einen hohen Anteil, eine dunkle Leiste mit hellem Text einen niedrigen.
- **Foto-Schutz:** Gewicht × (1 − bunt × Streuung). Bunt = mittlere Chroma (max − min), 0,10–0,25;
  Streuung = Standardabweichung der **Farben** (nicht der Luma), 0,06–0,15. Grauer Text auf Weiß
  streut, ist aber nicht bunt; eine einfarbige Schaltfläche ist bunt, streut aber nicht.
  Erst getestet mit Luma-Streuung: Gleich helle, bunte Flächen fielen durch.
- **Grenze des Foto-Schutzes:** Farbdetails unter ~4 px (feines Rauschen, kleine Muster) mittelt
  die 2 × 2-Probe zu Grau, das gilt dann nicht als Foto. Fotos mit Flächen ≥ 8 px werden erkannt
  (Test). Ein WARP-Testmuster mit 4-px-Kacheln genau auf dem Probenraster fiel deshalb durch.
- **Zeitliche Glättung:** zwei Texturen im Wechsel, neues Gewicht = `lerp(alt, neu, α)` mit
  `α = 1 − e^(−Δt / 50 ms)` (95 % nach ~150 ms, unabhängig von der Bildrate). Nach einer Pause
  `α = 1`: Die alte Textur wird gar nicht gelesen (nie geschrieben, kann NaN enthalten).
- **Nur bei Bedarf:** `SmartInvertLayer` läuft nur, wenn der Monitorfilter intelligent ist
  (`DisplayEffects.IsSmart`) oder ein Fensterbereich auf diesem Monitor (`WindowRegions.UsesSmart`).
- **Palette:** „intelligent“ hat dieselbe Matrix wie „Farbton erhalten“. Deshalb vergleicht
  `ChangesMonitor` zusätzlich den Index, sonst würde ein intelligentes Fenster auf einem
  Monitor mit „Farbton erhalten“ als „gleich“ gelten und das Overlay pausieren.
- **Reihenfolge in Listen:** `ColorFilters.DisplayOrder` (intelligent direkt nach Invertiert). Das
  Enum bleibt unverändert, weil gespeicherte Einstellungen und Paletten-Indizes daran hängen.
- **Rückfall (ADR-0024 Nr. 6):** `SmartInvertLayer.Create` legt Umgebungs-Durchgang und
  `GpuStopwatch` an; scheitert das (SharpGen-Fehler) → sofort Rückfall `gpu_feature`. Sonst misst
  `GpuStopwatch` den Umgebungs-Durchgang mit drei Sätzen Zeitstempel-Abfragen, gelesen mit
  `DoNotFlush` ein paar Frames später (nie warten). Mittel über 120 Frames > 1 ms → Rückfall
  `budget`. Danach lädt `DesktopLayer` die Palette mit `SmartEntry = NoEntry` hoch und setzt
  kein `MonitorSmart`: Intelligente Bereiche zeigen ihre Matrix, also „Farbton erhalten“. Gemessen
  wird nur der Umgebungs-Durchgang (der Zusatz im Magnify-Shader ist auf der RTX ~0,015 ms).
  Rückfälle stehen in `SmartInvertHealth` und auf der Diagnose-Seite; nach einem Neuaufbau der
  Pipeline wird neu gemessen.
- **GPU-Zeit:** siehe `performance.md` (Messprotokoll); Messung mit
  `ZOOMIN_GPU_TIMING=1 dotnet test --project tests/ZoomIN.Rendering.Tests --filter-method "*SmartInvertGpuTimeTests*"`,
  das Ergebnis steht in `%TEMP%\zoomin-gpu-timing.txt`.

## Debug-Schnappschuss (M13)
- Das Overlay ist aus jeder Aufnahme ausgenommen; auch Screenshots von Agenten zeigen nur das
  Original. Für die Sichtprüfung gibt es deshalb in „Allgemein → Diagnose“ den Knopf
  „Schnappschuss in 5 s“: Jeder Renderer, der gerade zeichnet, liest **vor `Present`** die
  Desktop-Kopie, den Back Buffer (Ergebnis inkl. Zeiger) und die Gewichte des intelligenten
  Invertierens zurück und schreibt nach `%LOCALAPPDATA%\ZoomIN\debug\snapshot-<Zeit>\`:
  `DISPLAYn-original.png`, `-result.png`, `-weights.png` (weiß = invertiert), `-info.txt`
  (Modus, Zoom, Filter, Fensterbereiche mit Palette – keine Titel).
- Ein pausierter Renderer (nichts gefiltert, kein Zoom) antwortet nicht; die Seite meldet dann
  „ZoomIN zeichnet gerade nichts“. Der Ordner kommt nie ins Diagnosepaket (Bildschirminhalt).
- Ohne neue Anforderung kostet `SnapshotTaker` je Frame nur einen Vergleich (0 B, Test). Achtung:
  Ein Lambda für `Task.Run` in derselben Methode ließ C# die Closure **bei jedem Aufruf** anlegen
  (32 B/Frame) – der teure Teil steht deshalb in einer eigenen Methode.
- PNG ohne Bibliothek: `PngWriter` (RGBA8, Filter 0, ein zlib-IDAT, CRC-32).

## Shader-Regeln
- HLSL-Dateien in `ZoomIN.Rendering/Shaders/`, als Embedded Resource eingebettet und beim
  Start mit dem in Windows enthaltenen D3DCompiler übersetzt (Entscheidung M1: kein SDK-Tool
  im Build/CI nötig). Vorkompilieren erst, falls die Startzeit es verlangt.
- Shader werden auf WARP getestet (`tests/ZoomIN.Rendering.Tests`).
- Max. 300 Zeilen pro Shader-Datei; Gemeinsames in `*.hlsli`.
