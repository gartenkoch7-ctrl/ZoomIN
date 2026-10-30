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

## Messprotokoll
| Datum | Hardware | Monitor | Modus | Ergebnis | Hinweis |
|---|---|---|---|---|---|
| 2026-10-30 | RTX 3060 Ti | 1080p, 60 Hz | Vollbild 2× (M1) | 60,4 fps (171 Frames / 2,8 s) | p99 noch nicht gemessen; Start per Hotkey ≈ 215 ms (Pipeline-Aufbau) |

## Bekannte Abweichungen (offen)
- **Allokation pro neuem Desktop-Frame:** `DesktopDuplicator.Update` erzeugt bei jedem
  geänderten Desktop-Bild zwei kleine COM-Wrapper (Vortice `IDXGIResource` +
  `QueryInterface<ID3D11Texture2D>`). Verstößt gegen „0 B im Render-Loop“ → in M2 messen und
  über Raw-Pointer-Aufruf oder gecachte Wrapper beseitigen.
- **Zoom an ≈ 215 ms** statt „nächster Frame“: M1 baut die Pipeline bei jedem Einschalten neu
  auf. M2 hält sie warm und blendet nur weich ein/aus.
