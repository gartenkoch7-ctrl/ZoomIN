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
- Kein `lock`, kein `Task`/`async` im Frame. Zustand per `Volatile.Read` eines Snapshots.
- GC: `<ServerGarbageCollector>false</ServerGarbageCollector>`,
  `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`; Render-Thread mit
  erhöhter Priorität und MMCSS (`AvSetMmThreadCharacteristics("Games")`).

## Messen
- **BenchmarkDotNet** mit `[MemoryDiagnoser]` für Core-Hot-Path (Viewport, Easing, Arbiter).
- **In-App-Overlay** (Debug-Build): Frame-Zeit, verpasste Frames, Duplication-Latenz.
- **PresentMon** / GPUView für echte Ende-zu-Ende-Messungen.
- Messergebnisse mit Hardware hier eintragen.
- Liegen Kopien des Repos unter `.claude/worktrees/`, bricht BenchmarkDotNet ab („more than one
  matching project file“). Dann mit `--inProcess` starten:
  `dotnet run -c Release --project tests/ZoomIN.Benchmarks -- --filter "*WindowRegions*" --inProcess --memory`.

## Messprotokoll
| Datum | Hardware | Monitor | Modus | Ergebnis | Hinweis |
|---|---|---|---|---|---|
| 2026-10-30 | RTX 3060 Ti | 1080p, 60 Hz | Vollbild 2× (M1) | 60,4 fps (171 Frames / 2,8 s) | p99 noch nicht gemessen; Start per Hotkey ≈ 215 ms (Pipeline-Aufbau) |
| 2026-10-30 | RTX 3060 Ti | 1080p, 60 Hz | M2, Pipeline warm | Hotkey → Overlay sichtbar **2–16 ms**; Leerlauf 0,5 % eines Kerns | Ausblenden bis unsichtbar ≈ 390 ms (siehe unten) |
| 2026-11-05 | Entwicklungsrechner | – | M11 Telemetrie-Start, Release, frischer Prozess, ohne ReadyToRun | Startpfad **~4 ms** Host, **~9 ms** inkl. erster Logzeile + Operation; ~100 ms Aufbau im Hintergrund | vorher (synchron) ~90 ms, kalter erster Lauf 255 ms – siehe unten |
| 2026-11-06 | RTX 3060 Ti, 20 Threads | 1080p, 60 Hz | M12 Filter pro Fenster, Dev-Build | CPU 0,00–0,05 % aller Kerne (helles Fenster, auch beim Verschieben); Popup → Bereich Median 3,3 ms, 90 % < 4,5 ms; `WindowRegionsCheck` 64 Bereiche 65 ns, 0 B | Bildrate nicht gemessen; vor dem Blick-Thread Popup bis 75 ms (ADR-0022) |

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
