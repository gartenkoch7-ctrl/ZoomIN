# Performance

## Budgets
| Metrik | Ziel |
|---|---|
| Bildrate | = Monitor-Hz (60/120/144/240), keine verpassten Frames |
| Latenz Desktop → Zoom | ≤ 2 Frames |
| Latenz Maus → Cursor im Zoom | ≤ 1 Frame |
| CPU-Zeit pro Frame (Render-Thread) | < 1 ms |
| GPU-Zeit pro Frame (4K, alle Filter) | < 2 ms auf Onboard-Grafik |
| Heap-Allokation im Render-Loop | **0 B** |
| Zoom an/aus | sichtbar im nächsten Frame |
| Leerlauf (Zoom aus) | Capture gestoppt, ~0 % CPU/GPU |

## Regeln im Hot Path
- Keine Allokationen: kein `new` für Referenztypen, kein LINQ, keine Lambdas mit Captures,
  keine String-Interpolation, kein Logging außer über vorab allokierte Ringpuffer.
- `struct`s für Werte (Punkte, Rechtecke, Snapshots-Inhalte), `readonly` wo möglich.
- GPU-Ressourcen werden nur bei Start/Resize/Monitorwechsel erstellt.
- Constant Buffer über `ConstantUpload<T>` füllen: Er vergleicht die Bytes mit dem letzten
  Upload und lässt gleiche Werte aus (M14). Das gilt nur, solange niemand anderes in den
  Puffer schreibt. Das Feld darf nicht `readonly` sein, sonst arbeitet es auf einer Kopie.
- `CursorPass` setzt den gemeinsamen Zustand einmal pro Ebene. Jede Ebene endet mit
  `End` (im `finally`), bevor ein anderer Durchgang zeichnet. Sonst übernimmt der nächste
  Zeichenaufruf auf dasselbe Ziel fremde Shader und fremdes Blending.
- Kein `lock`, kein `Task`/`async` im Frame. Zustand per `Volatile.Read` eines Snapshots.
- GC: `<ServerGarbageCollector>false</ServerGarbageCollector>`,
  `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`; Render-Thread mit
  erhöhter Priorität und MMCSS (`AvSetMmThreadCharacteristics("Games")`).

## Messen
- **BenchmarkDotNet** mit `[MemoryDiagnoser]` für Core-Hot-Path (Viewport, Easing, Arbiter).
- **In-App-Overlay** (Debug-Build): Frame-Zeit, verpasste Frames, Duplication-Latenz.
- **Startzeit** nie aus dem ersten Start nach einer Installation messen. Dort kosten kalter
  Dateicache und der erste Scan der neuen Dateien ein Vielfaches, bei `shell` z. B. 255 statt
  47 ms (M14, 2026-11-08). Den zweiten Start nehmen.
- **PresentMon** / GPUView für echte Ende-zu-Ende-Messungen.
- Messergebnisse mit Hardware hier eintragen.
- Liegen Kopien des Repos unter `.claude/worktrees/`, bricht BenchmarkDotNet ab („more than one
  matching project file“). Dann mit `--inProcess` starten:
  `dotnet run -c Release --project tests/ZoomIN.Benchmarks -- --filter "*WindowRegions*" --inProcess --memory`.

## Messprotokoll
| Datum | Hardware | Monitor | Modus | Ergebnis | Hinweis |
|---|---|---|---|---|---|
| 2026-11-11 | i7-12700KF | – | M17 A1 Randabstand (BenchmarkDotNet, Release, 4K-Monitor) | `AnchorMargin` 33 ns, `AnchorCenterMouse` 24 ns, `AnchorFollowCaret` 33 ns – alle 0 B (Stand nach Review: Moduswechsel, Platzierung) | Randabstand rechnet pro Frame Ausschnitt + Rückweg zum Anker (≈ +8 ns gegenüber Mitte); bei 144 Hz vernachlässigbar |
| 2026-10-30 | RTX 3060 Ti | 1080p, 60 Hz | Vollbild 2× (M1) | 60,4 fps (171 Frames / 2,8 s) | p99 noch nicht gemessen; Start per Hotkey ≈ 215 ms (Pipeline-Aufbau) |
| 2026-10-30 | RTX 3060 Ti | 1080p, 60 Hz | M2, Pipeline warm | Hotkey → Overlay sichtbar **2–16 ms**; Leerlauf 0,5 % eines Kerns | Ausblenden bis unsichtbar ≈ 390 ms (siehe unten) |
| 2026-11-05 | Entwicklungsrechner | – | M11 Telemetrie-Start, Release, frischer Prozess, ohne ReadyToRun | Startpfad **~4 ms** Host, **~9 ms** inkl. erster Logzeile + Operation; ~100 ms Aufbau im Hintergrund | vorher (synchron) ~90 ms, kalter erster Lauf 255 ms – siehe unten |
| 2026-11-06 | RTX 3060 Ti | 1440p (Offscreen) | M13 intelligent, Hysterese + Papier-Insel | Umgebung 0,071 ms; intelligent inkl. Umgebung 0,234 ms → **+0,094 ms** | Hochrechnung Onboard ≈ 0,9 ms – knapp am Budget; Messrechner hat keine iGPU (i7-12700KF) |
| 2026-11-06 | RTX 3060 Ti | 1440p (Offscreen) | M13 intelligent, Papier + Nachbarschafts-Durchgang | Umgebung (2 Schritte) 0,071 ms; intelligent inkl. Umgebung 0,221 ms → **+0,081 ms** | Hochrechnung Onboard ≈ 0,8 ms; Rückfall misst Umgebung ≈ 0,7 ms |
| 2026-11-06 | RTX 3060 Ti | 1440p (Offscreen) | M13 intelligent, Foto-Test 64 px | Umgebung 0,048 ms; intelligent inkl. Umgebung 0,222 ms → **+0,083 ms** | Hochrechnung ×10 Onboard ≈ 0,8 ms (Budget 1 ms, Rückfall misst Umgebung ≈ 0,5 ms) |
| 2026-11-06 | RTX 3060 Ti | 1440p (Offscreen) | M13 intelligent, kantenerhaltend (bilateral) | Magnify 0,140 ms; intelligent inkl. Umgebung 0,204 ms → **+0,064 ms** | Hochrechnung ×10 Onboard ≈ 0,6 ms (Budget 1 ms) |
| 2026-11-06 | RTX 3060 Ti | 1440p (Offscreen) | M13 intelligent, GPU-Zeitstempel, 300 Frames | Magnify 0,144 ms; Umgebung 0,031 ms; Magnify intelligent inkl. Umgebung 0,189 ms → **+0,046 ms** | 3 Läufe stabil; hochgerechnet ×10 auf Onboard ≈ 0,5 ms (Budget 1 ms), echte Onboard-Messung offen |
| 2026-11-07 | RTX 3060 Ti | 1440p (Offscreen) | M13 intelligent, Schrift folgt Umfeld (16 Nachbar-Taps) | überall gelesen: **+0,203 ms**; nur wo die 4 Kacheln uneins sind: intelligent inkl. Umgebung 0,223–0,238 ms → **+0,084–0,099 ms** | 3 Läufe; Taps nur an Kanten halten das Budget (≈ 1 ms Onboard hochgerechnet wie zuvor) |
| 2026-11-06 | RTX 3060 Ti, 20 Threads | 1080p, 60 Hz | M12 Filter pro Fenster, Dev-Build | CPU 0,00–0,05 % aller Kerne (helles Fenster, auch beim Verschieben); Popup → Bereich Median 3,3 ms, 90 % < 4,5 ms; `WindowRegionsCheck` 64 Bereiche 65 ns, 0 B | Bildrate nicht gemessen; vor dem Blick-Thread Popup bis 75 ms (ADR-0022) |
| 2026-11-07 | RTX 3060 Ti | 1080p/1440p (Offscreen), Zoom 1×/2×/4× | **M14 Messbasis** (`BaselineGpuTimeTests`, 200 Frames, 3 Läufe) | 1080p: Magnify 0,080 ms, Umgebung 0,035 ms, intelligent +0,053 ms – unabhängig vom Zoom. 1440p: Magnify 0,125–0,226 ms, Umgebung 0,067–0,100 ms, intelligent +0,095–0,181 ms | 1440p schwankt zwischen den Läufen (GPU-Takt); ein Ausreißerlauf (1,5 ms) verworfen. Kosten hängen kaum vom Zoom ab: Die Gewichtung läuft pro Ausgabe-Pixel |
| 2026-11-07 | i7-12700KF | – | **M14 Messbasis** CPU (BenchmarkDotNet, Release) | `LensFrame` 1,5 ns, `DockedFrame` 6,4 ns, `SmartRegionsCheck` (64 Bereiche) 22 ns, `PointerScale` 1,2 ns, `ModelRead` 0,5 ns, `EffectsRead` (2 Monitore) 6,7 ns – alle 0 B | Monitor-Suche per Name kostet < 7 ns pro Frame: der in M14 Schritt 2 geplante Monitor-Index lohnt nicht und entfällt |
| 2026-11-11 | i7-12700KF | – | M17 B2 Übersicht (BenchmarkDotNet, Release, `--inProcess`) | `OverviewFrameRect` 3,2 ns, `ViewRequestCheck` 0,1 ns – 0 B (nach A1/G1-Merge mit `PlacementFor`: 3,8 ns / 0,3 ns, 0 B); WARP-Test `OverviewLayerTests.UpdateAndDraw_DoNotAllocate` 0 B | Rahmen pro Frame ohne Allokation; Bildrate nicht gemessen (Sichtprüfung offen) |
| 2026-11-07 | i7-12700KF, RTX 3060 Ti | 1 Monitor 1440p | **M14 Start-Ausgangswert** (Trace des Nutzers, dev.119.m13) und Shader-Übersetzung (`ShaderSource` zur Laufzeit, 3 Runden) | `zoomin.app.start` 903 ms (darin `zoomin.settings.load` 109 ms); `zoomin.render.pipeline_start` 664 ms; davon Shader 570 ms (`PhotoFill` 347, `Magnify` PS 125, `Ambient` PS 81 ms) | Shader seit M14 Schritt 4 im Build übersetzt: die ~570 ms je Monitor und Pipeline-Start entfallen. Nachher-Wert aus einem echten Start offen |
| 2026-11-07 | i7-12700KF, RTX 3060 Ti | 1 Monitor 1440p | **M14 Startphasen** (Trace des Nutzers, dev.120.m14, Shader noch zur Laufzeit) | `zoomin.app.start` 565 ms: `services` 189 ms (davon Einstellungen 92 ms), `shell` 115 ms. `pipeline_start` 697 ms: `device` 18, `capture` 2, `overlay` 59, **`shaders` 617 ms** | Die Shader sind 89 % des Pipeline-Starts; mit Schritt 4 (dev.121) entfallen sie. Fensterfilter und Watchdog sind mit zusammen < 115 ms (`shell`) kein lohnendes Ziel mehr |
| 2026-11-07 | i7-12700KF, RTX 3060 Ti | 1 Monitor 1440p | **M14 Startphasen nachher** (Trace des Nutzers, dev.123.m14, Shader im Build übersetzt) | `pipeline_start` **184 ms** (vorher 697): `device` 41, `capture` 2, `overlay` 86, **`shaders` 6 ms** (vorher 617). `zoomin.app.start` 840 ms: `services` 198 (Einstellungen 92), `shell` 376 | Pipeline 3,8× schneller. `shell` lag in diesem einen Lauf höher als vorher (115 ms); er folgte direkt auf einen Update-Schritt, ein Einzelwert, beim nächsten Start gegenprüfen |
| 2026-11-08 | i7-12700KF, RTX 3060 Ti | 1 Monitor 1440p | **M14 Startphasen Gegenprobe** (Traces des Nutzers, zwei normale Starts: dev.124.m14 und dev.125.m14) | `pipeline_start` 146 / 123 ms: `device` 36 / 29, `capture` 2 / 2, `overlay` 57 / 42, `shaders` 5 / 7. `zoomin.app.start` 861 / 806 ms: `services` 244 / 242 (Einstellungen 105 / 104), `shell` 267 / 255 | Pipeline bleibt rund 5× schneller als vor Schritt 4. Die 376 ms `shell` von dev.123 waren ein Ausreißer, aber `shell` liegt stabil über den 115 ms von dev.120; `app.start` schwankt zwischen den Messungen stark (565–903 ms) |
| 2026-11-08 | i7-12700KF, RTX 3060 Ti | 1 Monitor 1440p | **M14 Shell-Phase aufgeteilt** (installierte dev.126.m14, zweiter Start nach der Installation, aus dem Terminal) | `zoomin.app.start` **354 ms**: `services` 191 (Einstellungen 93), `shell` **47**: `resolve` 15, `engine` 19, `tray` 4, `notify` 6. `pipeline_start` 43 ms: `device` 19, `capture` 2, `overlay` 14, `shaders` 6 | Die 255–376 ms `shell` der Messungen davor waren erste Starts direkt nach der Installation (kalter Cache, erster Scan). Kein Rückschritt durch DI; Startzeiten nur aus späteren Starts vergleichen |
| 2026-11-08 | i7-12700KF, RTX 3060 Ti | offscreen | **M14 Desktop-Kopie** (`CopyGpuTimeTests`, `ZOOMIN_GPU_TIMING=1`) | Ganze Textur (`CopyResource`): 1080p 0,033 ms, 1440p 0,048 ms, 4K 0,090 ms. Teilbereich (`CopySubresourceRegion`) 25 %: 0,010 / 0,013 / 0,025 ms; 5 %: 0,005 / 0,005 / 0,007 ms | Kopieren nur der Dirty Rects spart unter 0,04 ms je neuem Bild – entfällt. Onboard-Messung offen |

## Telemetrie-Start (M11)
Gemessen mit einem Wegwerf-Programm, das `TelemetryHost` in einem frischen Prozess startet:
- Synchron gebaut kosteten die Teile: Microsoft-`LoggerFactory` ~21 ms, OpenTelemetry-Logs
  ~22 ms, Tracer ~18 ms, erste Logzeile ~9 ms (fast alles JIT und Assembly-Laden). Mit
  ReadyToRun wäre es noch ~43 ms gewesen – über dem Budget von 20 ms (ADR-0019).
- Lösung: eigene `HostLoggerFactory` statt Microsofts `LoggerFactory`, OpenTelemetry-Pipelines per
  `Task.Run` im Hintergrund, bis dahin Logeinträge gepuffert (max. 500) und Abläufe über einen
  .NET-eigenen `ActivityListener` (`EarlyActivities`) aufgefangen und danach geschrieben.
- Folge: Ein sehr kurzlebiger Prozess wartet beim Beenden (`Dispose`) auf den Hintergrundaufbau
  (~100 ms) – nur bei Hilfsprozessen spürbar, nie beim Start.
- ReadyToRun für die App wäre eine eigene Entscheidung (größerer Download, schnellerer Start
  insgesamt) – nicht Teil von M11.

## Allokationen (M2 gelöst)
- `DesktopDuplicator.Update` ruft `AcquireNextFrame`, `ReleaseFrame` und `CopyResource` direkt
  über die COM-Vtable auf (`Rendering/Interop/DxgiInterop.cs`) – Vorticies Überladungen
  erzeugten pro Frame Wrapper-Objekte.
- Tests erzwingen 0 B: `Core.Tests/HotPath/AllocationTests` (Viewport, Animator, Modell) und
  `Rendering.Tests/Gpu/MagnifyPassAllocationTests` (Draw auf WARP).

## Offene Feinheiten
- **Ausblenden ≈ 390 ms:** Die Animation erreicht nach 150 ms 95 %, das Overlay verschwindet
  aber erst beim Einrasten auf exakt 1× (Schwelle 0,05 % in Log-Skala). Optisch endet die
  Bewegung früher; ggf. Schwelle anheben, wenn es sich träge anfühlt.
- **Leerlauf 0,5 %:** vermutlich der 250-ms-Status-Timer des Diagnosefensters (entfällt mit M7).
