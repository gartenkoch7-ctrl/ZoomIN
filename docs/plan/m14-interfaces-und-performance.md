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
| Performance-Umfang | Leerlauf-Frames + Lock pro Frame; Shader vorkompilieren; Smart-Invert-Kosten (nach Messung: nur das Budget, kein Umbau der Gewichtung, Grilling 2026-11-07). **Nicht:** Shader-Varianten (Permutationen) – erst wieder bei Bedarf |
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
- [x] Bedingung „nichts geändert“ in Core: `FrameChanges.NeedsRedraw`. Sie ist rein und
      getestet. Ein neuer Frame ist nötig bei:
  - neuem Desktop-Bild
  - geänderter Zeigerposition oder Zeigerform (nur auf dem eigenen Monitor)
  - bewegter Ansicht (Zoom-Animation, Gleiten, Modus)
  - neuer Snapshot-Referenz (Filter, Fenster, Tracking, Zeigeroptionen)
  - laufendem Locator oder laufender Smart-Invert-Glättung
  - angefordertem Schnappschuss
  - erstem Frame bzw. Frame nach dem Ausblenden
  - Vergleich mit dem zuletzt gezeigten Frame: `FrameChangeTracker` (Rendering, 0 B)
- [x] `RenderSession` lässt in diesem Fall Zeichnen **und** `Present` aus.
      DirectComposition zeigt den letzten Frame weiter.
- [x] Taktgeber im Leerlauf. Die Swapchain-Wartemarke wirkt wie eine Semaphore
      (`MaximumFrameLatency = 1`):
  - Ein Leerlauf-Durchgang behält den Platz, auf den er gewartet hat. Der nächste gezeichnete
    Frame wartet deshalb nicht bis zum Timeout von 100 ms.
  - Im Leerlauf wartet der Loop auf das VBlank des Monitors (`DesktopDuplicator.WaitForNextRefresh`).
    Schlägt das fehl oder kehrt es dreimal in Folge sofort zurück (Monitor aus), schläft er 8 ms.
    Der Aufruf geht direkt über die COM-Vtable (`DxgiInterop.WaitForVBlank`), weil Vortice bei
    Fehlern eine Ausnahme wirft und damit pro Durchgang allokieren würde.
  - Auch im Leerlauf bestätigt der Monitor mit dem Zeiger die gewünschte Sichtbarkeit des
    Systemzeigers (`CursorLayer.KeepSystemCursor`, lock-frei). Ein anderer Monitor kann ihn
    inzwischen umgeschaltet haben.
  - Ein Rückfall auf „Farbton erhalten“ erzwingt noch einen Frame, damit er sichtbar wird.
  - Nach jedem Takt prüft er Snapshots, Zeiger und Desktop-Bild erneut. Eine Änderung wird
    damit spätestens nach einem Bildschirmtakt gezeichnet.
- [x] Umgebungsdurchgang (Smart Invert) nur bei neuem Desktop-Bild oder geänderten Effekten.
  - Danach läuft er weiter, bis die Glättung eingeschwungen ist (6 τ = 0,3 s,
    `SmartInvertLayer.Settling`).
  - Auch beim Bewegen der Maus über einem ruhenden Desktop fällt er damit weg.
  - Die erste Änderung nach einer Ruhepause wird wie ein Frame geglättet, nicht auf einmal
    übernommen (gegen Flackern).
- [ ] Prüfen, ob sich das Kopieren nur der geänderten Bereiche lohnt (`GetFrameDirtyRects`/
      `MoveRects`) statt der ganzen Textur. Erst nach der Sichtprüfung; vermutlich klein,
      weil ruhende Frames jetzt gar nicht mehr kopieren.
- [x] Sichtprüfung durch den Nutzer (2026-11-07, dev.120.m14): „Alles in Ordnung“.
  - GPU-Last im Task-Manager bei ruhendem 2×-Zoom nahe null
  - Maus, Tippen, Scrollen und Video flüssig wie vorher
  - Lupe, Angedockt und intelligentes Invertieren ohne Flackern
- **Telemetrie:**
  - Neuer Zähler `zoomin.render.frames_skipped` (Tags `zoomin.monitor`, `zoomin.reason=idle`),
    Instrument beim Start. Kein Log pro Frame.
  - `zoomin.render.frame_interval` wird nicht über eine Leerlauf-Lücke hinweg erfasst.
  - `zoomin.render.frames_missed` zählt im Leerlauf nicht.
  - Die Bildrate im Status (Übersicht, Info-Seite) zählt Leerlauf-Durchgänge als pünktlich. Ein
    ruhender Zoom zeigt also weiter die Bildwiederholrate und nicht „0 Bilder pro Sekunde“.
  - `FrameStatistics` bündelt die Buchführung. Damit fällt `MonitorRenderer` von 257 auf
    230 Zeilen.
- **Tests:**
  - `FrameChangesTests`: Jede Änderung erzwingt einen Frame.
  - `FrameChangeTrackerTests`: Zeiger auf einem anderen Monitor ändert nichts, alle
    Snapshots, 0 B.
  - `SmartInvertSettleTests` (WARP): Die Glättung schwingt ein, ein neues Bild startet sie neu.
  - `DxgiInteropTests`: Der Vtable-Platz von `WaitForVBlank` wartet auf einem echten Monitor
    wirklich auf den nächsten Takt (übersprungen ohne Monitor).
  - Ein WARP-Test für den ganzen `RenderFrame` (Leerlauf, Wecken nach einem Takt) geht nicht:
    Er braucht Duplication und Swapchain eines echten Monitors. Die Teile sind einzeln
    getestet, das Ganze prüft der Nutzer bei der Sichtprüfung.

#### 4 · Schnellerer Start
- **Messbasis** (2026-11-07, Trace des Nutzers mit dev.119.m13, 1 Monitor):
  - `zoomin.app.start` 903 ms, darin Einstellungen laden 109 ms
  - `zoomin.render.pipeline_start` 664 ms, davon Shader-Übersetzung etwa 570 ms
    (`PhotoFill` 347 ms, `Magnify` PS 125 ms, `Ambient` PS 81 ms)
- [x] Shader zur Build-Zeit übersetzen:
  - Werkzeug `tools/ZoomIN.ShaderCompiler`: derselbe d3dcompiler_47 über Vortice mit denselben
    Flags, also kein SDK-Werkzeug in Build oder CI.
  - MSBuild-Target in `ZoomIN.Rendering.csproj`, inkrementell. Eingebettet wird nur der
    Bytecode; Vortice.D3DCompiler ist keine Laufzeit-Abhängigkeit mehr.
  - **Abweichung vom Plan:** kein Laufzeit-Rückfall und kein Log 3008. Fehlender Bytecode ist ein
    Build-Fehler, den `ShaderBytecodeTests` abfängt. Zur Laufzeit schlägt der Pipeline-Start
    mit Log 3003 fehl. Ein Rückfall hätte den D3D-Compiler weiter mitgeschleppt.
- [~] Smart-Invert-Ressourcen erst bei Bedarf anlegen: **entfällt**. Ohne Übersetzung sind es
      nur noch 3 Shader-Objekte und 3 kleine Texturen (1/16 Auflösung). Der Gewinn lag fast
      ganz in `PhotoFill`.
- [~] `Win32WindowFilterSource.Start` und Watchdog/`EaseOfAccessNotifier` verschieben:
      **entfällt nach Messung** (dev.120.m14, 2026-11-07). Die ganze Phase `zoomin.app.start.shell`
      dauert 115 ms, `services` 189 ms (davon Einstellungen laden 92 ms). Die Shader waren mit
      617 ms von 697 ms Pipeline-Start der eigentliche Brocken.
- [x] Nachher-Wert (dev.123.m14, 2026-11-07): `pipeline_start` 184 ms statt 697 ms, davon Shader
      6 ms statt 617 ms. `zoomin.app.start.shell` lag in diesem Einzellauf direkt nach einem
      Update-Schritt bei 376 ms (vorher 115 ms); beim nächsten Start gegenprüfen.
- **Telemetrie:** Die Kind-Abläufe aus Schritt 1 und `zoomin.render.ready_time` zeigen den
  Gewinn. Es gibt keine neuen Ereignisse.
- **Tests:**
  - `ShaderBytecodeTests`: Jeder `ShaderSource.Load` im Code ist als DXBC eingebettet, und ein
    fehlender nennt den `ShaderEntry`.
  - Alle WARP- und Offline-Tests laufen mit dem vorkompilierten Bytecode und sind
    unverändert grün.
  - Veröffentlichung geprüft: weder Werkzeug noch D3DCompiler im Paket.

#### 5 · Smart-Invert-Kosten
**Entscheidung (Grilling 2026-11-07): klein und sicher.**
- Schritt 3 spart den Umgebungsdurchgang bei ruhendem Desktop schon ein.
- Die Gewichtung in Desktop-Auflösung entfällt. Sie hätte das in M13 abgestimmte Aussehen von
  gezoomtem Text verändert, denn `SmartWeight` nutzt die *interpolierte* Helligkeit jedes
  Ausgabe-Pixels.
- Laut Messbasis hängen die Kosten kaum vom Zoom ab, der Gewinn wäre also klein.
- [x] Budget misst die echten Kosten: `SmartCostProbe` zeichnet je Pipeline 32 Frames lang
      den Desktop schlicht und intelligent nacheinander, beide mit GPU-Zeit.
  - Mehrkosten im Magnify-Durchgang = Differenz der Mittel. Der Rückfall (ADR-0024) prüft jetzt
    Umgebung plus diese Mehrkosten.
  - Vorher sah er nur den Umgebungsdurchgang und konnte für den teuren Teil nie greifen.
  - Gemessen wird erst nach 60 Frames mit Smart Invert (die GPU taktet nach dem Start oft noch
    niedrig). Wird der ganze Monitor intelligent, misst die Probe neu, denn das ist teurer als
    einzelne Fenster. Kommen zu wenige Messungen zurück, misst sie einmal erneut.
  - Die Probe-Zeichnungen überschreibt der echte Frame vollständig (Vollbild-Zeichnung oder
    Löschen), es ist also nichts davon zu sehen.
- [~] Gewichtung in Desktop-Auflösung, Scissor auf den Quellbereich: **entfällt** (siehe oben).
- [~] Lupe/Angedockt nicht doppelt zeichnen: **entfällt**. Es spart nur die Lupenfläche
      (etwa 12 %) und bräuchte vier beschnittene Zeichnungen statt einer.
- [x] `Magnify.hlsl` (285 Zeilen) unter 250 gebracht (156): Die Gewichtung steht in
      `SmartWeight.hlsli` (133). Das Werkzeug setzt `#include` selbst ein (`IncludeExpander`,
      mit `#line`), `.hlsli` sind Inputs des Targets (Schritt 10).
- **Telemetrie:**
  - `zoomin.render.smart_invert_gpu_time` = Umgebung + Mehrkosten im Magnify-Durchgang; Katalog
    angepasst.
  - Log 3007 beim Rückfall wie bisher (einmal je Pipeline).
- **Tests:** `SmartCostBudgetTests`
  - Umgebung und Mehrkosten zusammen über dem Budget → Rückfall
  - Mehrkosten allein → sofort Rückfall
  - beides darunter → bleibt intelligent
  - die Probe misst auf WARP einmal und hört dann auf
  - `RenderLayersAllocationTests` mit laufender Probe: 0 B
- **Ziel:** Die Hochrechnung auf Onboard-Grafik bleibt etwa 0,9 ms. Das Budget greift jetzt aber
  für die echten Kosten, statt sie zu übersehen. Eine echte Onboard-Messung ist weiter offen
  (kein Gerät).

### Teil B – Interfaces und DI (ADR-0025)

#### 6 · Grundlagen und Regel
- [x] Architekturtest nach ADR-0025 (`StaticClassTests`). Statische Klassen dürfen `File`,
      `Directory`, `Registry`, `Process`, `Environment.GetFolderPath`/`GetEnvironmentVariable`
      und `HttpClient` nicht nutzen. Statische `HttpClient`-Felder sind ebenfalls verboten.
  - Ausgenommen sind `Interop/`, `Composition/` und `Program.cs`.
  - Die Ausnahmeliste nennt je Fall Grund und Schritt (heute 15 Fälle).
    `Exceptions_AreNotStale` schlägt fehl, sobald ein Eintrag nicht mehr nötig ist; die Liste
    kann also nur schrumpfen.
- [x] Satz in AGENTS.md Abschnitt 2.6 (beim Plan angelegt)
- [x] `IAppFolders` (Core) mit `Local` und `Roaming`, die Dateinamen in `AppFiles` (Core, reine
      Pfadbildung) und die Implementierung `WindowsAppFolders` (Platform).
  - `Program` erzeugt sie einmal für alle Rollen; DI gibt sie weiter.
  - Umgestellt: `RecoveryFiles` (entfällt, Core liest keine Umgebung mehr), AppBar-Datei
    (Reservierung und Watchdog-Aufräumen), Einstellungen, Profile, Sicherungen, Telemetrie-Logs,
    Diagnosepakete, Debug-Schnappschüsse, Update- und Setup-Downloads, Info-Seite, Setup-Programm.
  - Sichere Bildschirme: `Local` liegt im eigenen SYSTEM-Profil (wie bisher `MachineFolder`), die
    Einstellungen des Sitzungsnutzers liest weiter `SessionUser` (Roaming des Nutzers, nur ohne
    SYSTEM aus `IAppFolders.Roaming`).
  - Noch nicht umgestellt, weil sie zu den Abläufen gehören: `CrashHistory.Default` und
    `SecureDesktopGiveUp` (Watchdog), die Löschpfade in `UninstallFlow`, das SYSTEM-Profil in
    `SecureDesktopInstall` und `Program Files` in `ElevatedInstall`. Das kommt in Schritt 7 und 8.
- [x] `TimeProvider` statt `Stopwatch.GetTimestamp()` in Core (`EffectsModel`, einzige Stelle).
  - Der Render-Loop (Rendering) misst weiter mit `Stopwatch`. Er ist kein Core; ein
    `TimeProvider` dort brächte nur virtuelle Aufrufe pro Frame und keine testbare Logik.
- [x] Ein `HttpClient` (`UpdateHttpClient`: User-Agent, kein globaler Timeout) aus DI für
      `GitHubReleaseFeed` (15 s je Aufruf) und `HttpPackageDownloader`. Im Setup gehört er
      `Program`.
- [x] `ILocalizer` mit Implementierung `Localizer` (Core, reine Wörterbuch-Suche) für App und Setup.
  - `Text` und `SetupText` sind Fassaden für `x:Bind`.
  - Die App-Instanz kommt aus DI. `AppShell` nutzt sie direkt, `Text.Use` setzt sie in der
    Composition Root.
- **Telemetrie:** keine neuen Ereignisse; Logs und Abläufe unverändert.
- **Tests:**
  - `StaticClassTests`
  - `LocalizerTests`: Sprachwechsel, Systemsprache, fehlender Schlüssel, Ereignis
  - `AppFilesTests`: Alle Dateien liegen dort, wo sie immer lagen.
  - `EffectsModelClockTests`

#### 7 · Plattform-Dienste
- [x] Interfaces in Core, Implementierungen in Platform, registriert in der Composition Root
      (`AddInstall`):
  - `IInstallRegistration` → `WindowsInstallRegistration` (bisher `InstallRegistration`)
  - `IShortcuts` → `WindowsShortcuts` (bisher `ShellShortcut`)
  - `ISecureDesktopInstall` → `SystemSecureDesktopInstall` (bisher `SecureDesktopInstall`)
  - `IElevatedStep` → `ShellElevatedStep` (bisher `ElevatedStep`, auch `HashOf`)
  - `IMessageDialog` mit `DialogAnswer` → `Win32MessageDialog` (bisher `MessageDialog`)
  - `IDiagnosticsPackage` → `DiagnosticsPackage` (Instanz mit `TelemetryHost` und `IAppFolders`)
  - `IWatchdogLauncher` (App) → `ShellWatchdogLauncher` (bisher `WatchdogStarter`). Die
    Implementierung bleibt in App, weil sie die Watchdog-Argumente kennt.
- [x] Klassen, deren einzige Nebenwirkung Dateien in den ZoomIN-Ordnern sind, werden
      Instanzklassen mit `IAppFolders` (testbar mit Testordnern, ohne eigenes Interface):
      `SecureDesktopGiveUp` (mit `TimeProvider`) und `CrashHistory` (`Default` entfällt).
- [x] `PngWriter` schreibt nur noch in einen Stream; die Datei öffnet der Aufrufer (`SnapshotTaker`).
- [x] `SessionUserFolders`: bereits in Schritt 6 erledigt (nur noch `IsSystem`).
- [~] Gemeinsamer `IAtomicFileWriter`: **entfällt**. Es sind drei Zeilen je Store. Ein Dienst
      hätte jeden Store und das Setup um einen Konstruktor-Parameter erweitert und mehr
      gekostet, als er spart.
- **Telemetrie:** Die Abläufe `zoomin.update.elevate`, `zoomin.diagnostics.package` und die Logs
  der Dienste sind unverändert. Nur die Log-Kategorie folgt jetzt dem Typnamen der Dienste.
- **Tests:**
  - `WindowsShortcutsTests` (echte .lnk im Testordner)
  - `DiagnosticsPackageTests` mit Testordnern
  - Ausnahmeliste von `StaticClassTests` von 15 auf 5 Fälle geschrumpft
  - Registry- und Adminschritte sind ohne Admin nicht testbar; ihre Abläufe testet Schritt 11
    mit gefälschten Diensten.

#### 8 · Abläufe als Klassen
- [x] In Schritt 7 vorgezogen, weil sie die einzigen Nutzer der neuen Dienste sind:
      `UninstallFlow`, `UpdateRunner` (App) und `ElevatedInstall` (Setup) sind Instanzklassen mit
      Diensten im Konstruktor.
- [x] `Program.RunFlow` baut für Update und Deinstallation einen kleinen Container
      (`ServiceRegistration.BuildRole`). Das Setup hat `SetupServices` als handgeschriebene
      Composition Root. Dort liegt auch das Installationsziel `Program Files\ZoomIN`.
- [x] `SecureDesktopHost` und `WatchdogRunner` sind Instanzklassen (Einstieg ihrer Rolle).
  - Vom Schalter für den Anmeldebildschirm bleibt nur das Parsen statisch
    (`SecureDesktopSetting`). Der Schalter selbst ist der Dienst `ISecureDesktopSwitch`
    (`SecureDesktopSwitch`): in der Einstellungsseite über `PageContext`, im Admin-Schritt über
    `RunFlow`.
  - `RestartArguments` parst nur noch; das Warten auf die alte Version macht `Program`.
  - Die Ausnahmeliste von `StaticClassTests` hat nur noch einen Eintrag: `Win32TrayIcon`
    (Schritt 10).
- [x] Nahtstellen für die Ablauf-Tests aus Schritt 11 (Review 2026-11-07):
  - `IRunningInstall` (Core, `ProcessRunningInstall` in Platform, über `AddInstall`) ersetzt
    `Environment.ProcessPath` in `UninstallFlow` und `UpdateRunner`.
  - `IRemovalSteps` (`ProcessRemovalSteps`): Admin-Schritt mit runas, andere ZoomIN beenden,
    Ordner nach Prozessende löschen lassen.
  - Den Autostart schaltet `UninstallFlow` über `IAutostart` aus DI ab, jetzt auch nur als
    Versuch: ein Registry-Fehler bricht die Deinstallation nicht mehr ab.
  - `WatchdogRunner` bekommt `IWatchdogSystem` (`ProcessWatchdogSystem`: auf den Prozess warten,
    neu starten, Tray-Hinweis, Pause) und `TimeProvider`.
  - Noch mit `Environment.ProcessPath` lesen die Installation `SecureDesktopSwitch`,
    `UpdateService` und `RegistryAutostart`; für diese Abläufe gibt es bisher keine Tests. Den
    eigenen Pfad zum Starten von Prozessen nutzen `ProcessRemovalSteps`, `ShellWatchdogLauncher`,
    der Watchdog-Start in `Program` und `SetupInstaller`. Das sind die echten Implementierungen
    hinter den Nahtstellen.
  - Log 1206 (`RemovalFailed`) hat jetzt die Kategorie `ProcessRemovalSteps` statt `UninstallFlow`,
    die EventId bleibt gleich.
- **Tests:** in den neuen Testprojekten (Schritt 11)
  - Deinstallation: Reihenfolge, Abbruch, Fehler beim Entfernen
  - Update: Rückgabewerte, Adminschritt abgelehnt
  - Watchdog: Neustart-Politik, Aufräumen, Aufgeben auf sicheren Bildschirmen
- **Testbuild 2026-11-07 (Schritte 7 und 8):**
  - dev.122.m14 per Skript installiert. Die installierte dev.122 hat sich danach über ihren
    neuen Update-Schritt auf dev.123.m14 aktualisiert: Exit-Code 0, Versionsordner umgestellt,
    Apps-Eintrag 1.0.0-dev.123.m14, Traces ohne Fehler.
  - Nutzer: Start, Anmeldebildschirm-Schalter, sichere Instanz (Win+L), Diagnosepaket und
    Info-Seite „Alles in Ordnung“.
  - Nicht geprüft: Deinstallation über Windows „Apps“ (entfernt die Installation).

#### 9 · Mapper nach Core
- [x] `ZoomMapping` (Core/Zoom), `EffectsMapping` (Core/Filters) und `TrackingMapping`
      (Core/Tracking), bisher `ZoomModelFactory`, `EffectsFactory` und `TrackingFactory` in App.
  - Die 19 Zeigerfelder stehen einmal in `PointerMapping` (Core/Pointer), in beide Richtungen.
    `EffectsMapping` nutzt es.
- **Tests:** `SettingsMappingRoundTripTests`
  - Settings → Modell → Settings gibt jeden gemappten Wert zurück, auch die Zoomstufen pro
    Monitor. Verglichen wird nach exaktem Property-Namen, mit Werten abseits der
    Werkseinstellung; vergisst ein Mapper ein Feld, schlägt der Test an.
  - Ausnahme ist `ZoomOn`: Jeder Start beginnt ungezoomt, `ZoomMapping.Create` übernimmt es
    bewusst nicht.
  - Unlesbare Farbe → Standardfarbe.

### Teil C – Struktur und Duplikate

#### 10 · Große Klassen aufteilen
- [x] `ZoomController` (12 Konstruktor-Parameter) aufgeteilt:
  - `ZoomCommands`: Zoom, Modus und Filter für den Monitor unter der Maus
  - `WindowFilterCommands`: Fenster-Filter-Kürzel, offene Fensterarten
  - `HotkeyBindings`: Tabelle Geste → Aktion, registrieren/aussetzen, nicht belegbare Kürzel
  - `InputSources`: Parameterobjekt für Mausrad, Aktivität, Tracking, Fensterfilter
  - `DockedStripSync` kommt aus DI.
  - Übrig bleibt `ZoomController` für Engine-Start, Status und Ende mit 6 Parametern.
- [x] `PageContext` (Record mit 14 Feldern) entfällt.
  - Seiten bekommen `PageServices` als Navigationsparameter. Es baut jedes View-Model mit genau
    den Diensten, die sein Konstruktor nennt (`ActivatorUtilities`); Seiten holen wenige
    Dienste direkt (Zeigerbilder, Status).
  - Die Rückrufe ins Fenster (Übersicht nach dem Erststart, Beenden fürs Update, Fenster-Handle
    für Dateidialoge) sind `IShellActions`, umgesetzt von `AppShell`.
  - `AppShell` sinkt von 14 auf 11 Parameter.
- [x] `AppShell` weiter aufgeteilt: `TrayMenuSync` (Menü, Tooltip, Zoom- und Profilbefehle,
      Fensterfilter-Hinweis), `StartupNotices` (Update- und UIAccess-Hinweis). `AppShell` behält
      Fenster, Erststart und Beenden, 8 Parameter. `ModeName` steht jetzt in der `Text`-Fassade.
- [x] `MonitorRenderer.cs` unter 250 Zeilen (230, Schritt 3)
- [x] `AnchorController.cs` (244 → 199): Rechteck-Mathe in `AnchorGeometry`, `Move` ohne doppelte
      Zweige. `Win32WindowFilterSource.cs` (236 → 191): WinEvent-Hooks in `WindowEventHooks`.
- [x] Gemeinsame `LowLevelHookThread` für `Win32WheelHook` und `Win32InputActivityHook`. Die
      statische Rückruf-Weiterleitung bleibt nach ADR-0025 Punkt 1. `Dispose` schickt `WM_QUIT`
      nur einmal: ein zweites ginge an eine Thread-Id, die Windows schon neu vergeben haben kann.
- [x] `TrayIconImage` ist eine Instanz mit dem Icon-Pfad; die Ausnahmeliste von
      `StaticClassTests` ist leer.
- [x] Testbuild dev.124.m14 (2026-11-07): Sichtprüfung durch den Nutzer „Alles in Ordnung“
      (Tray-Menü, Fenster und Beenden, Mausrad, Strg antippen, Fokus/Caret, Fensterfilter,
      Smart-Invertierung, Info-Seite). Start ohne Fehler: `app.start` 880 ms, `app.start.shell`
      272 ms, `pipeline_start` 152 ms, davon Shader 5,5 ms (mit `SmartWeight.hlsli`).
- **Telemetrie:** Die Logs bleiben bei ihren Klassen bzw. Feature-Ordnern (EventIds unverändert).
  Die Log-Kategorie der Kürzel folgt jetzt `HotkeyBindings`; die von 1006 (UIAccess) und 1007
  (Update-Prüfungen) folgt `StartupNotices` statt `AppShell`.
- **Tests:** Hotkey-Tabelle, Fensterfilter-Befehle, Tray-Befehle und „jedes View-Model lässt
  sich aus dem Container bauen“ (App.Tests, Schritt 11).

#### 11 · Testprojekte
- [x] `tests/ZoomIN.App.Tests`, `tests/ZoomIN.Watchdog.Tests`, `tests/ZoomIN.Setup.Tests`
      (xUnit v3, Microsoft.Testing.Platform wie die anderen), in `ZoomIN.slnx`; CI testet die
      ganze Solution.
  - App und Setup sind eigenständige WinUI-Exes. Die Testprojekte setzen deshalb
    `ValidateExecutableReferencesMatchSelfContained=false` und schalten die Start-Initialisierer
    des Windows App SDK ab. Die kollidierten sonst über `InternalsVisibleTo` mit denen der App.
  - Es wird kein WinUI-Fenster erzeugt.
- [x] Testdateien halten die 300-Zeilen-Regel ein (`check-file-length.ps1` prüft `tests/` mit).
      Die Architekturtests prüfen nur `src/`, die Testprojekte brauchen dort nichts.
- [x] Erste Tests (23):
  - App: `ContainerTests` (jedes View-Model, jeder Konstruktor-Parameter ist registriert, genau
    ein Konstruktor; der ganze Graph jeder Rolle mit `ValidateOnBuild`), `HotkeyBindingsTests` (Tabelle, Wiederholung, nicht belegte und vergebene
    Kürzel, erneutes Registrieren, Aussetzen), `CommandLineTests` (`RestartArguments`,
    `SecureDesktopSetting`)
  - Watchdog: „aufgegeben“-Marke bis zum Neustart von Windows, Absturzliste (20 Zeilen),
    `WatchdogArguments`
  - Setup: `SetupChoices.ApplyTo` (übernimmt die Wahl, behält den Rest, Filter aus) und die
    `SetupText`-Fassade
- [x] Ablauf-Tests mit gefälschten Diensten (19):
  - `UninstallFlowTests`: nicht installiert, Abbrechen, abgelehnte Admin-Abfrage, Einstellungen
    behalten oder löschen, Fehler beim Aufräumen beim Nutzer (bricht nicht ab), Admin-Schritt
  - `UpdateRunnerTests`: nicht installiert, abgelehnte Signatur, installiert mit Apps-Eintrag und
    Anmeldebildschirm, Fehler beim Apps-Eintrag. Die beiden letzten brauchen Verzeichnis-Links
    (Admin oder Entwicklermodus) und werden sonst übersprungen.
  - `WatchdogRunnerTests`: nicht überwachbar, reguläres Ende, Absturz mit Aufräumen und Neustart,
    vierter Absturz im Fenster mit Hinweis, Abstürze außerhalb des Fensters, Anmeldebildschirm
    mit Pause, Schalter und stiller Aufgabe

## Akzeptanzkriterien
- [x] Leerlauf: Ruhender Desktop bei 2× → GPU-Last nahe null, `frames_skipped` steigt.
      Mausbewegung ohne spürbare Verzögerung (Sichtprüfung durch den Nutzer 2026-11-07, dev.120.m14).
- [x] Start bis zur fertigen Pipeline gegenüber der Messbasis messbar kürzer (697 → 184 ms); keine
      Laufzeit-Übersetzung der Shader mehr (Schritt 4, gemessen an dev.123.m14)
- [x] Kein Lock und keine Allokation im Render-Loop (Benchmarks und WARP-Tests `0 B`, Code-Review; Teil A)
- [x] Smart Invert: Das Budget umfasst Umgebung und Mehrkosten im Magnify-Durchgang (Grilling
      2026-11-07: kein Umbau der Gewichtung, Ziel ≤ 0,6 ms entfällt); Offline-Wiedergabe unverändert
- [x] Architekturtest ADR-0025 grün mit leerer Ausnahmeliste (Schritt 10)
- [x] App, Watchdog und Setup haben Testprojekte mit Ablauf-Tests für Deinstallation, Update und
      Watchdog-Neustart (Schritt 11; Update zusätzlich im Testbuild echt geprüft)
- [x] `MonitorRenderer.cs` und `Magnify.hlsl` unter 250 Zeilen; `AnchorController.cs` und
      `Win32WindowFilterSource.cs` ≤ 200; `check-file-length.ps1` grün
- [x] Telemetrie im Katalog `docs/kb/telemetrie.md`, Messwerte in `docs/kb/performance.md`

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
