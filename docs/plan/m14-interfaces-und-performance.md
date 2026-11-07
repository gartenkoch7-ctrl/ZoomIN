# M14 – Interfaces, DI und Performance

**Ziel:** ZoomIN wird spürbar sparsamer und startet schneller. Danach wird der Code testbarer:
- Ein ruhender Bildschirm kostet fast keine GPU mehr.
- Shader werden nicht mehr zur Laufzeit kompiliert.
- „Invertiert (intelligent)“ liegt sicher im Budget.
- Statische Klassen mit Nebenwirkungen werden zu Diensten mit Interfaces und DI (ADR-0025).
- Große Klassen werden aufgeteilt.
- App, Watchdog und Setup bekommen eigene Testprojekte.

**Demo:**
- **Leerlauf:** Bei 2× Vollbild-Zoom auf einem ruhenden Desktop fällt die GPU-Last im
  Task-Manager auf nahe null. Bewegt man die Maus, ist alles sofort flüssig wie vorher.
- **Start:** Der Start bis zum ersten Zoom ist messbar schneller. Das zeigt das Histogramm
  `zoomin.render.ready_time`; die einzelnen Phasen stehen unter `zoomin.app.start` und
  `zoomin.render.pipeline_start`.
- **Tests:** `dotnet test` läuft auch für Deinstallation, Update und Watchdog. Gefälschte Dienste
  ersetzen dabei Registry und Prozesse.

**Voraussetzungen:** M13 (intelligentes Invertieren) gemergt.
→ [ADR-0025](../adr/0025-statische-klassen-nur-ohne-nebenwirkungen.md)

## Entscheidungen (Grilling 2026-11-07)
| Thema | Entscheidung |
|---|---|
| Umfang static → DI | nur Klassen mit Nebenwirkungen (Dateien, Registry, Prozesse, Dialoge, Netz, Umgebungsvariablen, Pfade, Uhr, Sprache); reine Logik, Logs, Telemetrie, Interop, Einstiegspunkte bleiben static |
| Sprache | `ILocalizer` als Dienst (App und Setup gemeinsam); `Text` bleibt dünne Fassade für `x:Bind` |
| Reihenfolge | **Performance zuerst** (Teil A), dann DI (Teil B), dann Aufteilen und Duplikate (Teil C) |
| Grundsatz | ADR-0025 + Architekturtest + Satz in AGENTS.md Abschnitt 2 |
| Performance-Umfang | Leerlauf-Frames + Lock pro Frame; Shader vorkompilieren; Smart-Invert-Kosten. **Nicht:** Shader-Varianten (Permutationen) – erst wieder bei Bedarf |
| Tests | neue Projekte `ZoomIN.App.Tests`, `ZoomIN.Watchdog.Tests`, `ZoomIN.Setup.Tests` |
| Render-Loop | Dienste nur über den Konstruktor als `readonly`-Feld, nie pro Frame aus dem Container; Mathe bleibt static |

## Ausgangslage (Analyse 2026-11-07)
- **Static:**
  - Es gibt rund 110 statische Klassen, etwa 20 davon mit Nebenwirkungen.
  - Globaler Zustand: `Text`/`SetupText`, zwei `static HttpClient`, `Stopwatch` in `EffectsModel`.
  - Core liest in `RecoveryFiles` mit `Environment.GetFolderPath` die Umgebung.
- **Performance:**
  - Jeder Frame wird voll gezeichnet und mit `Present` angezeigt, auch ohne Änderung
    (`RenderSession.cs`).
  - `CursorLayer` ruft pro Frame `ISystemCursor.SetVisible` auf. Das nimmt jedes Mal einen
    `lock`, den alle Render-Threads teilen (`MagnifierSystemCursor`).
  - Neun Shader-Kompilierungen pro Monitor zur Laufzeit (`ShaderSource`, O3) ohne Cache.
  - Die Smart-Invert-Gewichtung läuft pro Ausgabe-Pixel. Bei Zoom Z ist das Z²-mal zu oft.
  - Die Budget-Messung erfasst den teuren Teil in `Magnify.hlsl` nicht.
- **Struktur:**
  - `ZoomController` hat 12 Konstruktor-Parameter, `AppShell` 10. `PageContext` ist faktisch
    ein Service-Locator.
  - `MonitorRenderer.cs` hat 256 Zeilen, `Magnify.hlsl` 285.
  - Duplikate: zwei Hook-Klassen, zwei `HttpClient`-Fabriken, etwa 16 Stellen bauen die Pfade
    `%LocalAppData%\ZoomIN` (Logs, Wiederherstellung, Abstürze) und `%AppData%\ZoomIN`
    (Einstellungen, Profile) zusammen, dreimal „.tmp schreiben und verschieben“.
- **Tests:** Etwa 40 % der Typen sind ungetestet. App, Setup und Watchdog haben kein Testprojekt.

## Aufgaben

### Teil A – Performance (zuerst)

#### 1 · Messbasis (vorher messen)
- [x] Neue Benchmarks (`FrameGeometryBenchmarks`): `ModeGeometry.Compute` (Lupe, Angedockt),
      `WindowRegions.UsesSmart`, `PointerStyle.ScaleFor`/`DrawsPointer`, alle 0 B.
  - `FrameMapping`, `WindowConstants.Fill` und die Konstanten-Builder liegen intern in
    Rendering. Das Benchmark-Projekt kennt nur Core, deshalb decken die WARP-Allokationstests
    sie ab.
- [x] Allokationstests mit `0 B` für `CursorLayer` (Zeiger, Fadenkreuz, Locator, beide
      Zeigerarten), `HighlightLayer` und `DesktopLayer` (Vollbild und Lupe, intelligent) mit
      GPU-Zeitmessung (`RenderLayersAllocationTests`).
  - `RenderFrame` selbst braucht Duplication und Swapchain eines echten Monitors und läuft
    nicht auf WARP. Es ist über seine Ebenen abgedeckt.
- [x] GPU-Zeitstempel im Betrieb für Magnify (`GpuPassTimer`); die Umgebung misst
      wie bisher `SmartInvertLayer`. Offscreen-Messung 1080p/1440p, Zoom 1×/2×/4×, intelligent
      an und aus (`BaselineGpuTimeTests`, nur mit `ZOOMIN_GPU_TIMING=1`).
- [x] Startzeit messen:
  - App-Phasen `services` und `shell` als Kind-Abläufe von `zoomin.app.start`.
  - Gerät, Capture, Overlay und Shader als Kind-Abläufe von `zoomin.render.pipeline_start`.
  - Statt „bis erster Frame“ gibt es `zoomin.render.ready_time` (Prozessstart bis Pipeline
    bereit). Den ersten Frame gibt es erst beim ersten Zoom; er misst also den Nutzer, nicht
    den Start.
- [x] Ausgangswerte in `docs/kb/performance.md` (Messprotokoll 2026-11-07).
- [ ] Startzeit-Ausgangswert aus einem echten Start (Diagnosepaket nach Neustart durch den
      Nutzer: `ready_time`, Phasen). Er ist die Vergleichsbasis für Schritt 4.
- **Telemetrie** (Instrumente beim Start angelegt, im Katalog):
  - Kind-Abläufe `zoomin.app.start.services`/`.shell` und
    `zoomin.render.pipeline_start.device`/`.capture`/`.overlay`/`.shaders`
  - Histogramm `zoomin.render.ready_time` in ms (Tag `zoomin.monitor`, einmal je Prozess und
    Monitor)
  - Histogramm `zoomin.render.gpu_time` in ms (Tags `zoomin.monitor` und `zoomin.pass` mit
    `magnify`; `weights` kommt mit Schritt 5). Abfragen nur mit Abonnent; kein Gesamt-Frame-Wert,
    weil verschachtelte Disjoint-Abfragen nicht überall erlaubt sind (Review)
- **Ergebnis:**
  - Die Kosten von Smart Invert hängen kaum vom Zoom ab.
  - 1440p schwankt auf der RTX zwischen den Läufen (+0,095–0,181 ms).
  - Die Monitor-Suche pro Frame kostet unter 7 ns.

#### 2 · Lock und kleine Arbeit pro Frame
- [x] Lock pro Frame entfernt: `MagnifierSystemCursor.SetVisible` prüft den Zustand zuerst ohne
      Lock und nimmt den Lock nur bei einer echten Änderung.
  - Der Cache sitzt bewusst in der Implementierung und nicht in `CursorLayer`. Alle
    Render-Threads teilen den einen Systemzeiger, und ein Cache je Monitor würde veralten,
    wenn ein anderer Monitor den Zeiger umschaltet.
- [~] Monitor-Index statt Dictionary-Suche: **entfällt nach Messung** (Schritt 1: unter 7 ns pro
      Frame, 0 B).
- [ ] `CursorPass`: letzte Zeigertextur zwischenspeichern; Blend-State einmal pro Ebene statt
      pro Zeichnen
- [ ] Constant Buffer nur hochladen, wenn sich die Werte geändert haben (`MagnifyPass`,
      `CursorPass`)
- **Tests:** Benchmarks aus 1 bleiben bei `0 B`.
  - `CursorLayer` ruft `SetVisible` weiter jeden Frame auf. Der Test zählt die Aufrufe mit
    einem gefälschten `ISystemCursor`.
  - `MagnifierSystemCursor` ist nicht automatisch testbar, weil er den echten Zeiger des Nutzers
    ausblenden würde. Code-Review und Sichtprüfung decken ihn ab.

#### 3 · Leerlauf-Frames überspringen
- [ ] Bedingung „nichts geändert“ in Core. Sie ist rein und testbar und hält alles fest, was
      einen neuen Frame nötig macht:
  - kein neues Desktop-Bild
  - Zeiger und Zeigerform gleich
  - Animation eingeschwungen
  - gleiche Snapshot-Referenz
  - kein Locator, keine Glättung im Lauf
  - kein Schnappschuss angefordert
- [ ] `RenderSession` lässt in diesem Fall Zeichnen **und** `Present` aus.
      DirectComposition zeigt den letzten Frame weiter.
- [ ] Taktgeber im Leerlauf (siehe Risiko). Die Swapchain-Wartemarke wirkt wie eine Semaphore
      (`MaximumFrameLatency = 1`). Ohne `Present` blockiert `WaitForNextFrame` bis zum Timeout
      von 100 ms und zählt `frames_missed` hoch. Deshalb:
  - Die Leerlauf-Bedingung wird **vor** dem Warten geprüft. Im Leerlauf entfällt das Warten
    auf die Swapchain.
  - Stattdessen wartet der Loop in Scheiben von höchstens einem Bildschirmtakt:
    `WaitForVBlank`, oder `AcquireNextFrame` mit dem Timeout einer Frame-Zeit.
  - Beide Aufrufe lassen sich nicht von außen unterbrechen. Deshalb prüft der Loop nach jeder
    Scheibe:
    - die Snapshot-Referenz (Hotkey, Zoom, Filter)
    - Zeigerposition und Zeigerform
    - ein neues Desktop-Bild
  - Eine Änderung wird spätestens nach einem Bildschirmtakt gezeichnet.
- [ ] Umgebungsdurchgang (Smart Invert) nur bei neuem Desktop-Bild, oder bis die Glättung
      eingeschwungen ist (etwa 3 τ)
- [ ] Prüfen, ob sich das Kopieren nur der geänderten Bereiche lohnt (`GetFrameDirtyRects`/
      `MoveRects`) statt der ganzen Textur. Nur umsetzen, wenn die Messung aus 1 es trägt.
- **Telemetrie:**
  - Neuer Zähler `zoomin.render.frames_skipped` (Tags `zoomin.monitor`, `zoomin.reason=idle`),
    Instrument beim Start. Kein Log pro Frame.
  - `zoomin.render.frame_interval` wird nicht über eine Leerlauf-Lücke hinweg erfasst. Sonst
    erscheint jede Pause als Ruckler.
  - `zoomin.render.frames_missed` zählt im Leerlauf nicht.
  - Beide Änderungen kommen in den Katalog.
- **Tests:**
  - Unit-Tests für die Bedingung (jede Änderung erzwingt einen Frame)
  - WARP-Tests:
    - Ein ruhender Desktop zeichnet nach dem Einschwingen keinen Frame mehr.
    - Eine Mausbewegung zeichnet spätestens nach einem Bildschirmtakt.
    - Eine Zoom- oder Filteränderung **im Leerlauf** zeichnet spätestens nach einem
      Bildschirmtakt, nicht erst nach 100 ms.
  - Benchmark `0 B`

#### 4 · Schnellerer Start
- [ ] Shader zur Build-Zeit kompilieren (Bytecode `.cso` als eingebettete Ressource).
  - Zuerst ein Spike: Werkzeug (fxc aus dem Windows SDK oder ein kleines Build-Tool mit
    Vortice) und der Schritt in MSBuild/CI.
  - Rückfall: ein Bytecode-Cache je (Datei, Einstieg, Profil) im Prozess.
- [ ] Smart-Invert-Ressourcen (3 Shader, 3 Texturen, Abfragen) erst bei der ersten Nutzung von
      „intelligent“ anlegen
- [ ] `Win32WindowFilterSource.Start` blockiert nicht mehr bis zu 2 s. Ist der Filter pro Fenster
      aus, wird er gar nicht erst gestartet.
- [ ] Watchdog-Start, `EaseOfAccessNotifier` und Tray-Menütexte erst nach dem ersten Frame;
      `Process`-Objekt der Startzeitmessung freigeben
- **Telemetrie:**
  - Die Abläufe und `ready_time` aus 1 zeigen den Gewinn.
  - Neues Log 3008 (Warnung, einmal je Prozess), falls der Rückfall greift:
    `Shader bytecode for {Shader} missing; compiling at runtime`.
  - Der Eintrag kommt in den Katalog.
- **Tests:** Test, dass jede eingebettete Shader-Ressource Bytecode für alle Einstiege enthält.
  WARP-Tests laufen mit vorkompiliertem Bytecode.

#### 5 · Smart-Invert-Kosten
- [ ] Gewichtung (`SmartWeight`) in einem eigenen Durchgang in **Desktop-Auflösung**, begrenzt
      auf den Quellbereich plus Rand. Sie wird zwischengespeichert, solange sich der Desktop
      nicht ändert. `Magnify.hlsl` liest danach nur noch einen Wert.
- [ ] Scissor/Viewport des Umgebungsdurchgangs auf Quellbereich plus Rand begrenzen (beim Zoom
      1/Z² der Arbeit)
- [ ] Lupe/Angedockt: Den Lupenbereich nicht doppelt zeichnen (Scissor im 1×-Durchgang)
- [ ] `GpuStopwatch` misst Umgebung **und** Gewichtung. Der Rückfall auf „Farbton erhalten“
      greift damit für die echten Kosten.
- [ ] Ergebnis ohne sichtbare Änderung: Die Offline-Wiedergabe (`OfflineRenderTests`,
      `SmartInvert*Tests`) bleibt pixelgleich bzw. innerhalb der Toleranz.
- [ ] `Magnify.hlsl` dabei unter 250 Zeilen bringen (Gewichtung in eigene `.hlsli`/`.hlsl`)
- **Telemetrie:**
  - `zoomin.render.smart_invert_gpu_time` misst heute nur den Umgebungsdurchgang (Mittel über
    120 Frames). Künftig misst es Umgebung **plus** Gewichtung, und das Budget prüft diesen Wert.
  - Die Aufteilung je Durchgang liefert `zoomin.render.gpu_time` mit dem Tag `zoomin.pass`
    (aus Schritt 1).
  - Den Katalogtext anpassen.
  - Log 3007 beim Rückfall wie bisher (einmal je Pipeline).
- **Ziel:** Hochrechnung Onboard bei 1440p und 2× ≤ 0,6 ms (heute etwa 0,9 ms). Die Zahl wird nach der
  Messbasis aus Schritt 1 bestätigt.

### Teil B – Interfaces und DI (ADR-0025)

#### 6 · Grundlagen und Regel
- [ ] Architekturtest nach ADR-0025: Statische Klassen dürfen `File`, `Directory`, `Registry`,
      `Process`, `Environment.GetFolderPath`/`GetEnvironmentVariable` und `HttpClient` nicht nutzen.
  - Zuerst mit einer Ausnahmeliste, die alle heutigen Fälle enthält. Sie schrumpft mit jedem
    Schritt und ist am Ende von M14 leer bis auf begründete Fälle.
- [ ] Satz in AGENTS.md Abschnitt 2 (neuer Unterabschnitt „Statische Klassen“)
- [ ] `IAppFolders` (Interface in Core, Implementierung in Platform) ersetzt die etwa 16
      Pfad-Stellen. Es kennt beide Orte:
  - Lokal (`%LocalAppData%\ZoomIN`): Logs, Wiederherstellung, Abstürze, Diagnose
  - Roaming (`%AppData%\ZoomIN`): Einstellungen, Profile
  - Auf sicheren Bildschirmen gelten die Ordner des Sitzungsnutzers.
  - `RecoveryFiles` in Core bekommt die Pfade von außen.
- [ ] `TimeProvider` statt `Stopwatch.GetTimestamp()`/`DateTime.UtcNow` in Core und in den
      Diensten. Render-Loop: einmal im Konstruktor, Benchmark `0 B`.
- [ ] Ein `HttpClient` aus DI (User-Agent zentral; Timeout je Aufruf) für `GitHubReleaseFeed` und
      `HttpPackageDownloader`
- [ ] `ILocalizer` (Sprache, `Get`, `Format`, Ereignis `LanguageChanged`) gemeinsam für App und
      Setup. `Text` und `SetupText` werden zur Fassade über den Dienst.
- **Telemetrie:** keine neuen Ereignisse. Die bestehenden Logs ziehen mit um (gleiche EventIds).
- **Tests:** Architekturtest; Tests für `ILocalizer` (Sprachwechsel, fehlender Schlüssel) und
  `IAppFolders` (sichere Bildschirme: Profil des Sitzungsnutzers).

#### 7 · Plattform-Dienste
- [ ] Diese statischen Klassen werden zu Interfaces mit Implementierung, jeweils in der
      Composition Root registriert:
  - `InstallRegistration`
  - `ShellShortcut`
  - `SecureDesktopInstall`
  - `ElevatedStep`
  - `MessageDialog`
  - `DiagnosticsPackage`
  - `SessionUserFolders`
  - `WatchdogStarter`
  - `SecureDesktopGiveUp`
- [ ] Gemeinsamer `IAtomicFileWriter` (.tmp schreiben, dann `File.Move`) für `JsonSettingsStore`,
      `JsonProfileStore` und `SecureSettingsStore`
- **Telemetrie:** Die Abläufe `zoomin.update.*`, `zoomin.securedesktop.*` und
  `zoomin.diagnostics.*` bleiben gleich. Im Katalog wird der Ort aktualisiert.
- **Tests:** Platform-Tests der Implementierungen, wo es ohne Admin geht (temporäre Ordner,
  HKCU-Testschlüssel).

#### 8 · Abläufe als Klassen
- [ ] Diese Abläufe werden zu Instanzklassen mit Diensten im Konstruktor:
  - `UninstallFlow`
  - `UpdateRunner`
  - `SecureDesktopHost`
  - `SecureDesktopSetting`
  - `WatchdogRunner`
  - `ElevatedInstall`
- [ ] `Program.RunRole` baut je Rolle einen kleinen Container über dieselben `Add*`-Erweiterungen.
      Bisher bekommt jede Rolle nur einen rohen `ILogger`.
- **Tests:** in den neuen Testprojekten (Schritt 11)
  - Deinstallation: Reihenfolge, Abbruch, Fehler beim Entfernen
  - Update: Rückgabewerte, Adminschritt abgelehnt
  - Watchdog: Neustart-Politik, Aufräumen, Aufgeben auf sicheren Bildschirmen

#### 9 · Mapper nach Core
- [ ] `ZoomModelFactory`, `EffectsFactory` und `TrackingFactory` wandern als Settings-Mapper nach
      Core (Ordner nach Feature). Die etwa 25 Zeigerfelder bekommen einen eigenen Mapper statt
      Feld-für-Feld-Kopien in zwei Richtungen.
- **Tests:** Rundreise-Tests (Settings → Modell → Settings ist identisch) für alle drei.

### Teil C – Struktur und Duplikate

#### 10 · Große Klassen aufteilen
- [ ] `ZoomController` wird aufgeteilt in:
  - `HotkeyBindings` (Tabelle Geste → Aktion)
  - `WindowFilterCommands`
  - `EngineStarter`
  - Ziel: höchstens 6 Konstruktor-Parameter
- [ ] `AppShell` wird aufgeteilt in Tray-Befehle, Fenster/Erststart und Update-Hinweise.
      `PageContext` entfällt: Seiten und ViewModels kommen aus dem Container und bekommen nur,
      was sie brauchen.
- [ ] `MonitorRenderer.cs` (256) unter 250 Zeilen bringen
- [ ] `AnchorController.cs` (244) und `Win32WindowFilterSource.cs` (236) nach Verantwortung
      aufteilen, Ziel ≤ 200 Zeilen. Beide stehen kurz vor der Teilungsgrenze.
- [ ] Gemeinsame `LowLevelHookThread` für `Win32WheelHook` und `Win32InputActivityHook`. Die
      statische Rückruf-Weiterleitung bleibt nach ADR-0025 Punkt 1.
- **Telemetrie:** Die Logs bleiben bei ihren Klassen bzw. Feature-Ordnern (EventIds unverändert).
- **Tests:** Hotkey-Tabelle, Fensterfilter-Befehle, Tray-Befehle (App.Tests)

#### 11 · Testprojekte
- [ ] `tests/ZoomIN.App.Tests`, `tests/ZoomIN.Watchdog.Tests`, `tests/ZoomIN.Setup.Tests`
      (xUnit, Microsoft.Testing.Platform wie die anderen), in `ZoomIN.slnx` und CI
- [ ] Architekturtests kennen die neuen Projekte. Testdateien halten die 300-Zeilen-Regel ein.
- [ ] Erste Tests: `RestartArguments`, `HotkeyFormat`, `SettingsService`, `ProfileService`,
      `WatchdogArguments`, plus die Tests aus 8–10

## Akzeptanzkriterien
- [ ] Leerlauf: Ruhender Desktop bei 2× → GPU-Last nahe null, `frames_skipped` steigt.
      Mausbewegung ohne spürbare Verzögerung (Sichtprüfung durch den Nutzer).
- [ ] Start bis zum ersten Frame gegenüber der Messbasis aus 1 messbar kürzer; keine
      Laufzeit-Kompilierung der Shader mehr
- [ ] Kein Lock und keine Allokation im Render-Loop (Benchmarks `0 B`, Code-Review)
- [ ] Smart Invert: GPU-Messung umfasst Umgebung und Gewichtung; Hochrechnung Onboard ≤ 0,6 ms;
      Offline-Wiedergabe unverändert
- [ ] Architekturtest ADR-0025 grün mit leerer bzw. begründeter Ausnahmeliste
- [ ] App, Watchdog und Setup haben Testprojekte; Deinstallation, Update und Watchdog getestet
- [ ] `MonitorRenderer.cs` und `Magnify.hlsl` unter 250 Zeilen; `AnchorController.cs` und
      `Win32WindowFilterSource.cs` ≤ 200; `check-file-length.ps1` grün
- [ ] Telemetrie im Katalog `docs/kb/telemetrie.md`, Messwerte in `docs/kb/performance.md`

## Offene Fragen / Risiken
- **Leerlauf und Swapchain-Takt:** Ohne `Present` wird die Wartemarke der Swapchain nicht
  freigegeben. Das Warten blockiert bis zum Timeout von 100 ms. Der Takt im Leerlauf muss
  deshalb aus `AcquireNextFrame` oder `WaitForVBlank` kommen, in Scheiben von einem Takt.
  Nach jeder Scheibe werden Snapshot und Zeiger geprüft. Prüfen, ob
  DirectComposition nach längerer Pause (Sperre, Monitor aus) sauber weiterzeigt und ob die
  Latenz beim ersten Frame nach dem Leerlauf steigt.
- **Leerlauf und Zeigerform:** Die Zeigerform kann sich ohne Bewegung ändern (Sanduhr). Die
  Bedingung muss sie mitprüfen.
- **Vorkompilierte Shader:** Werkzeug im Build (fxc ist Teil des Windows SDK, nicht von .NET).
  CI braucht es ebenfalls. Bytecode für Feature-Level 10_0 (`ps_4_0`) muss reichen.
- **Gewichtung in Desktop-Auflösung:** Sie braucht eine zusätzliche Textur je Monitor. Die
  Kanten-Logik (Striche, Kanten) muss pixelgleich bleiben. Das Risiko trägt die
  Offline-Wiedergabe.
- **`Text`-Fassade:** `x:Bind` auf statische Eigenschaften bleibt. Ein Sprachwechsel zur
  Laufzeit muss weiter alle Seiten neu laden (heute über `LanguageChanged`).
- **Zeitpunkt:** Teil A ist vor `v1.0.0` sinnvoll, weil das Budget von M13 knapp ist. Teil B
  und C blockieren 1.0 nicht. Die Entscheidung trifft der Nutzer beim Start von Teil B.
- **Nicht im Umfang:** Shader-Varianten (Permutationen) von `Magnify.hlsl`. Erst wieder, wenn
  die Messung nach Schritt 5 es verlangt.
