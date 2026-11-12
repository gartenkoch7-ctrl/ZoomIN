# Architektur

## Projekte
```
ZoomIN.sln
src/
  ZoomIN.Core/          Reine Logik, keine Plattformabhängigkeit
  ZoomIN.Platform/      Win32, UI Automation, Hooks, Monitore, DPI, Hotkeys
  ZoomIN.Rendering/     D3D11, Desktop Duplication, Shader, Overlay-Swapchain
  ZoomIN.App/           WinUI 3, Tray, Einstellungen, Composition Root
  ZoomIN.Watchdog/      Kleiner Überwachungsprozess (ab M3, ADR-0007)
tests/
  ZoomIN.Core.Tests/
  ZoomIN.Rendering.Tests/   Shader-Tests auf WARP (ab M1)
  ZoomIN.Architecture.Tests/
  ZoomIN.Benchmarks/
```

## Prozesse
Ein Prozess (`ZoomIN.exe`) mit UI-Thread, Render-Thread(s), Hook- und UIA-Threads, plus
`ZoomIN.Watchdog.exe`, der nach einem Absturz aufräumt und neu startet (ADR-0007).

## Abhängigkeitsregeln
| Projekt | darf referenzieren |
|---|---|
| Core | – (nur BCL) |
| Platform | Core |
| Rendering | Core |
| App | Core, Platform, Rendering |
| Watchdog | Core |
| Speech (später) | Core |

Platform und Rendering sprechen nur über Interfaces aus Core miteinander; verbunden in App.

## Ordnerstruktur (nach Feature)
```
ZoomIN.Core/
  Zoom/          ZoomState, ZoomEasing, ZoomLimits
  Viewport/      ViewportCalculator, PhysicalPoint, PhysicalRect
  Modes/         IZoomMode, FullscreenMode, LensMode, DockedMode
  Tracking/      ITrackingSource, TrackingArbiter (wer gewinnt: Maus/Fokus/Caret)
  Filters/       ColorFilterSettings, ColorMatrix
  Settings/      AppSettings, Profile, ISettingsStore
  Monitors/      MonitorInfo, IMonitorProvider
ZoomIN.Platform/
  Interop/       P/Invoke, CsWin32
  Input/         MouseTracker, Hotkeys
  Automation/    FocusTracker, CaretTracker (UIA)
  Monitors/      Win32MonitorProvider, DPI
  Settings/      JsonSettingsStore
ZoomIN.Rendering/
  Interop/
  Capture/       DesktopDuplicator (pro Monitor)
  Overlay/       OverlayWindow (click-through, Capture-Ausschluss)
  Gpu/           Device, Swapchain, Shader-Pipeline
  Shaders/       *.hlsl
  RenderLoop/    RenderThread, FrameScheduler
ZoomIN.App/
  Composition/   DI-Setup
  Tray/
  Views/         Settings-Seiten (XAML)
  ViewModels/
```

## Datenfluss pro Frame
```
Platform (Maus/Fokus/Caret) ─► Core.TrackingArbiter ─► Core.ViewportCalculator
                                                             │
                                                    RenderSnapshot (immutable)
                                                             ▼
Rendering: DesktopDuplicator ─► Shader (Scale + Filter + Cursor) ─► Overlay-Swapchain
```
- UI ändert Settings → neuer `RenderSnapshot` → atomarer Referenztausch.
- Render-Thread liest pro Frame genau einen Snapshot; keine Locks.

## Vorübergehende Zoomstufe (M17, `TemporaryZoom`)
Gemeinsamer Baustein für Übersicht (B2) und Fenster-Verschieben (G1). `ZoomModel.Temporary` hält
pro Monitor und **Besitzer** (`TemporaryZoomOwner.Overview`, `.WindowDrag`) eine Stufe.
- **Deckel, keine feste Stufe:** Der Monitor zeigt `min(Stufe des Nutzers, vorübergehende Stufe)`.
  1× hebt den Zoom auf; „absenken auf X nur wenn höher“ ist einfach `Set(…, X)`. Bei mehreren
  Besitzern gilt die niedrigste.
- **Zwei Werte:** `TargetFor` = Wahl des Nutzers (Einstellungen, Statustexte, `IsOn`),
  `EffectiveTargetFor` = was der Monitor zeigt (liest der Render-Loop; der KI-Debug-Zustand
  zeigt beide). Das Gleiten hinein und heraus macht der vorhandene `ZoomAnimator`.
- **Nie gespeichert:** `ZoomLevels` und `settings.json` sehen die vorübergehende Stufe nicht.
- **Regeln:** Nur der Besitzer gibt frei (`Release`, `ReleaseAll`). Ein Mausrad-/Tastenschritt,
  der den Zoom anlässt, ändert die Stufe des Nutzers darunter (wird wie immer gespeichert), die
  vorübergehende Stufe bleibt. Befehle des Nutzers löschen, damit nie ein Monitor auf 1× hängen
  bleibt: `Toggle`/`Activate` und `SetMode` mit anderem Modus alle Stufen, ein Schritt von oder
  auf 1× die Stufen der geschalteten Monitore. Besitzer merken das über `Temporary.Changed` und
  `LevelFor(...) == null`.
- **Angedockt:** keine Stufen – der Wechsel dorthin löscht sie, `Set` tut dort nichts
  (`LevelFor` bleibt null). Der Streifen reserviert Bildschirmfläche, bei 1× bliebe
  sie leer.
- **Ereignis:** `Temporary.Changed` ist getrennt von `ZoomModel.Changed`, damit eine
  vorübergehende Stufe kein Speichern der Einstellungen auslöst. `ZoomEngine` weckt die Renderer
  auf beide.
- **Monitorwechsel:** `Move(owner, von, nach)` verschiebt die Stufe in einer Änderung (hält der
  Besitzer dort schon eine, bleibt die niedrigere). Stufen hängen an der Monitor-ID; ändert sich
  die Monitoranordnung, gibt der Besitzer mit `ReleaseAll` frei.
- **Bewusst so:** Ein Schritt während der vorübergehenden Stufe ist zunächst unsichtbar (die
  Übersicht kann damit die Stufe fürs Hineinzoomen wählen). Bleibt eine Stufe hängen, räumt
  `Strg+Alt+Z` sie ab (erster Druck: aus, zweiter: wieder an mit der Stufe des Nutzers).
- **Telemetrie:** nur der Zähler `zoomin.zoom.temporary_levels`; Logs und Abläufe mit Monitor
  bringen die Besitzer (B2, G1) in ihren EventId-Bereichen mit.
- Pro Frame: ein zusätzlicher Wörterbuch-Lookup ohne Lock, `0 B`
  (`AllocationTests.ZoomModel_Reads_DoNotAllocate`, Benchmark `ModelReadTemporary`).

## Ansicht auf einen Punkt zentrieren (M17, `ViewCenterRequests`)
Naht zwischen Funktionen und dem Anker (B2 Übersicht, A1 Randabstand). Eine Funktion ruft
`ViewCenterRequests.Request(monitor, P, Zoom, At)`; der Render-Thread dieses Monitors gibt
`PlacementFor(monitor)` als `AnchorInput.Placement` (`ViewPlacement`, A1) an den
`AnchorController`. Der verankert so, dass `P` bei der Zielstufe `Zoom` in der Bildmitte steht (am
Monitorrand begrenzt), hält das, solange der Zoom dorthin gleitet; danach gilt der Randabstand.
- Liegt in `RenderModels.ViewRequests` (ein Objekt je Prozess). Neueste Anfrage gewinnt. `At` ist
  ein Stopwatch-Zeitstempel und steigt immer (`Request` erzwingt das): ein neuerer Wert ist für den
  Anker eine neue Anfrage; ein Renderer nimmt die beim Start vorhandene nicht als neu.
- Pro Frame ohne Lock und ohne Allokation (`OverviewAllocationTests`, Benchmark
  `ViewRequestCheck`). Weg: `MonitorRenderer` → `FrameRequest.Placement` → `RenderSession` →
  `AnchorInput`.
- Wirkt nur im Modus „Randabstand“ (Standard), nicht angedockt. In „Mitte“ und „Fixpunkt“
  entscheidet sofort die Maus – dort liegt die Ansicht ohnehin um die Maus.
- Schreiber heute: Übersicht beim Hineinzoomen (Rahmenmitte, Stufe des Nutzers) und bei Esc (Mitte
  der Ansicht vor der Übersicht). Die Anfrage kommt **vor** dem Freigeben der 1×, damit das Gleiten
  schon zum Punkt geht. `OverviewPlacementTests` prüft, dass der Anker dann genau den Rahmen zeigt.

## Einstellungen und Dateigröße (M18)
- `AppSettings` erbt die gemeinsamen Eingabe-Einstellungen (Tastenkürzel, Mausrad-Modifier,
  Haltetaste) vom abstrakten Basis-Record `InputSettings`. Grund ist nur die 300-Zeilen-Regel.
  System.Text.Json schreibt geerbte Eigenschaften auf dieselbe oberste Ebene, das Dateiformat bleibt
  gleich (`JsonSettingsStoreTests.Save_KeepsInheritedInputSettingsAtTheTopLevel`).
- Verschachtelte Einstellungsobjekte würden das Format ändern und bräuchten eine Migration.
- `SettingsService.SaveNow` speichert immer nur einmal gleichzeitig. Sonst schreiben Debounce-Timer,
  Sofort-Speichern und das Löschen eines Schemas dieselbe `.tmp`-Datei des aktiven Profils.

## Muster
- DI über `Microsoft.Extensions.DependencyInjection`, nur in App konfiguriert.
- MVVM in App (`CommunityToolkit.Mvvm`).
- Ereignisse aus Platform werden als Werte (structs) weitergereicht, nicht als Objekte.
