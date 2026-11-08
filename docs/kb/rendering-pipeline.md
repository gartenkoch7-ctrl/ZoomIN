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
- **Zwei Durchgänge:** `Ambient.hlsl` (`AmbientPass`) schreibt je 16 × 16 Desktop-Pixel Gewicht und
  Hintergrund-Luma in eine `R16G16_FLOAT`-Textur (1440p → 160 × 90). `Magnify.hlsl` liest sie
  kantenerhaltend (siehe „Kanten scharf“) in **Desktop-UV** und mischt zwischen „Original“ (Palette 1, nur Kontrast/Helligkeit) und
  „Invertiert, Farbton erhalten“. In Desktop-UV gemessen, damit beim Zoomen nichts umkippt.
- **Entscheidung je Texel:** 4 × 4 Proben im Abstand von 8 px, jede genau zwischen vier Pixeln
  (bilinear = ihr Mittel) → Umfeld 32 × 32 px, Nachbar-Texel überlappen. Eine Probe zählt hell ab Luma 0,55–0,75; die
  Mehrheit entscheidet (siehe „Kanten scharf“). Text auf Weiß behält eine helle Mehrheit, eine
  dunkle Leiste mit hellem Text eine dunkle.
- **Kanten scharf (Fix 2026-11-06, Hover-Fehler in der Systemsteuerung):** Bilinear gelesene
  Gewichte gaben an jeder Hell/Dunkel-Kante einen 16–32 px breiten, halb invertierten grauen Saum
  (sichtbar z. B. am Hover-Kasten und am Rand dunkler Fenster). Jetzt: (1) Jede Kachel entscheidet
  **binär** nach Mehrheit (Gewicht 1 = heller Hintergrund, 0 = dunkler) und speichert die Luma
  ihres **Hintergrunds** (Mittel der Mehrheit, nicht aller Proben) – `R16G16_FLOAT`. (2) Magnify
  nimmt je Pixel die vier Nachbarkacheln, gewichtet bilinear **und** nach Ähnlichkeit zwischen
  Pixel-Luma und Hintergrund-Luma (σ = 0,12, gemeinsame bilaterale Hochskalierung). Ein heller
  Pixel an der Kante folgt der hellen Kachel, ein dunkler der dunklen – Übergang 1–2 px statt
  16–32. Weich bleibt nur noch die Zeit (Glättung 50 ms).
- **Schrift an der Kante folgt ihrem Umfeld, nicht ihrer Farbe** (Excel, 2026-11-07): Schrift hat
  die Farbe der *anderen* Seite. Excel im dunklen Design: helle Zeilennummern auf dunkler Spalte
  direkt neben dem weißen Blatt passten per eigener Luma zur Blatt-Kachel und wurden mitinvertiert
  (fast unsichtbar); dunkle Zelltexte 4 px neben der Spalte passten zur dunklen Kachel und blieben
  dunkel auf Schwarz. Jetzt prüft Magnify **nur wo die vier Kacheln uneins sind** (sonst keine
  Zusatzkosten) 8 Nachbarn im Abstand 3 px: Weniger als 3 ähnliche (±0,1) → Strich. Ist er auch
  in 6 px Abstand fast ringsum (≥ 6 von 8) anders, stimmen diese fernen Nachbarn über die Kachel ab
  statt der eigenen Luma. Abstand 2 px zerfraß Buchstaben (ihr Innenraum galt als Strich); nur
  3 px Abstimmung machte aus den 1–2 px Lücken fetter Schrift (Blattregister) weiße Punkte – daher
  der ferne Ring und die „ringsum“-Bedingung. Rest: In der nur ~20 px hohen aktiven
  Registerkarte („Tabelle1“) bleibt die grüne Schrift teils dunkel und unter der Grundlinie stehen
  einzelne helle Pixel – die Insel ist kleiner als eine Entscheidungskachel. Test:
  `SmartInvertTextTests`.
- **Kachel-Zuordnung:** Pixel → Kachel über `uv · TextureSize / 16`, nicht `uv · Kachelzahl` –
  die Textur hat `ceil(Breite / 16)` Kacheln, das driftete bis zu einer Kachel über den Monitor
  (bei 1121 px: dunkle Pixel neben einer Kante wurden invertiert). `WEIGHT_BLOCK` im Shader =
  `AmbientPass.Block` (Test).
- **Offline-Wiedergabe für Agenten:** Original per Screenshot (Overlay fehlt darin ohnehin) →
  PowerShell/WPF nach rohem BGRA (`int Breite, int Höhe, Pixel`) → `ZOOMIN_OFFLINE_INPUT=<datei>
  dotnet test --project tests/ZoomIN.Rendering.Tests --filter-method "*OfflineRenderTests*"` →
  `<datei>-result.png` und `-weights.png` daneben. So lässt sich der Filter ohne den Nutzer prüfen
  (die ZoomIN-Oberfläche nimmt wegen UIAccess keine Klicks von Agenten an). Bilder nur in
  `TestResults/` (gitignored), nie ins Repo.
- **Foto-Schutz (überarbeitet 2026-11-06):** eigener Test über ein **64 × 64**-Umfeld (4 × 4 Proben
  im Abstand von 16 px): Foto, wenn mindestens die Hälfte der Proben bunt ist (Chroma 0,10–0,25)
  und die Farben streuen (0,06–0,15). Dann Gewicht 0 und Hintergrund = mittlere Luma dieses
  Umfelds. Nur für Kacheln mit hellem Hintergrund (dunkle werden ohnehin nicht invertiert).
  Vorher wurde die mittlere Farbigkeit über 32 px gemessen und weich gemischt: Symbole (≤ 48 px)
  und blauer Linktext galten halb als Foto, das gab **graue Höfe** um jedes Symbol, auffällig
  beim Hovern in der Systemsteuerung. Symbole werden jetzt wie alles andere farbtonerhaltend
  invertiert.
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

### Papier, Fotos und Scrollen (selbstständiger Test 2026-11-06)
- **Zwei Schritte je Frame:** `Ambient.hlsl` entscheidet je Kachel roh (RGBA16F: Papier,
  Hintergrund-Luma, Foto, „hell ohne Papier“), `PhotoFill.hlsl` entscheidet mit den Nachbarn und
  glättet über die Zeit (R16G16F, wie Magnify sie liest).
- **Papier statt Mehrheit:** Heller Hintergrund heißt helle Mehrheit **und** ≥ 6/16 Proben innerhalb
  0,04 unter der hellsten Probe (ein einheitlicher Papierton). Gemessen auf echten Aufnahmen: Text,
  Seiten, fette Überschriften, Systemsteuerung 8–13/16; Schnee in einem Foto 1–3/16 (auch wo 12/16
  hell sind); ein Symbol 4/16. Eine reine Mehrheitsschwelle trennte das nicht: 50 % kehrte Schnee
  in Fotos um (schwarze Flecken), 60 % ließ fette Überschriften zerfallen. Der „Glattanteil“ um
  den Mittelwert taugte ebenfalls nicht (Fließtext 1/16 wie Schnee).
- **Hell ohne Papier** (dichter Text, Rahmenlinie, Symbol – oder Foto): Seite, wenn auf einer
  Seite Papier liegt und gegenüber Papier oder eine dunkle Fläche (bis 32 px), sonst original.
  Am Fotorand liegt gegenüber Foto → bleibt original.
- **Foto:** Mehrheit eines 64-px-Umfelds bunt; helle Kacheln zusätzlich mit Farbstreuung. Dunkle
  bunte Flächen (blauer Himmel) zählen ohne Streuung – dunkle UI ist grau, kein Himmel. Kacheln mit
  Papier sind **nie** Foto (sonst blieb die Seite neben einem bunten Balken weiß). Foto-Kacheln
  melden die Luma ihres Inhalts ohne Papier. Löcher: Kacheln ohne Papier mit Foto auf zwei
  gegenüberliegenden Seiten (bis 64 px) und Papier-Kacheln mit Foto auf ≥ 3 Seiten (ausgebranntes
  Weiß; eine Seite darf grauer Fels sein) bleiben original.
- **Zeitliche Glättung nur bei ähnlichem Hintergrund:** Springt die Hintergrund-Luma einer Kachel
  um > 0,25 (Inhalt hat sich bewegt), gilt die neue Entscheidung sofort. Scroll-Test
  (`OfflineScrollTests`, Inhalt wandert je 60-Hz-Frame): vorher bis 7,4 % (12 px/Frame) bzw. 2,1 %
  (4 px/Frame) abweichende Pixel durch nachgezogene graue Geister, jetzt ≤ 1,0 % bzw. 0,8 %.
- **Werkzeuge:** `ZOOMIN_OFFLINE_PROTECT=0` (Schutz aus), `ZOOMIN_OFFLINE_ZOOM=3` (Mitte
  vergrößert), `ZOOMIN_OFFLINE_SCROLL=<px>` (Scroll-Test mit Bericht), dazu `-decisions.png`
  (rot = Papier, grün = Foto, blau = hell ohne Papier). Mit diesen Karten wurden alle Fälle hier
  gefunden – erst messen, dann Regeln ändern.
- **Exakte Pixel (verlustfrei):** `scripts/capture-offline.ps1` im Terminal-Panel (Nutzersitzung; die
  Agenten-Sandbox kann den Bildschirm nicht kopieren) schreibt `TestResults/offline/<name>.raw`.
  Nur Bereiche aufnehmen, die kein fremdes Fenster überdeckt (Computer-Use-Maske beachten). JPEG
  war zu gnädig: mit exakten Pixeln ist ausgebrannter Schnee **Papier** (6/16) – gelöst durch
  „Papier-Insel“: eine Papier-Kachel mit < 2 direkten hellen Nachbarn (ohne Foto) ist keine Seite.
  Hell ohne Papier ist Seite bei Papier in zwei Richtungen (z. B. links und oben, Symbol darunter).
- **Video (`OfflineVideoTests`, `ZOOMIN_OFFLINE_VIDEO=x,y,b,h`, `ZOOMIN_OFFLINE_PAN`):** Fotobereich
  bekommt je Frame neues Rauschen (±8) und schwenkt optional. Ohne Hysterese flackerte ausgebrannter
  Schnee an der Papier-Grenze (still: 1,6 % falsch, 1,3 % Kippen je Frame). **Hysterese** in
  `PhotoFill.hlsl`: War die Kachel original **und** liegt derselbe Inhalt dort (Inhalts-Luma ±0,1),
  braucht sie für „Seite“ Papier ≥ 8/16, ≥ 3 Seiten-Nachbarn und Papier in zwei Richtungen; war sie
  Seite bei gleicher Papierfarbe, bleibt sie es bis < 4/16. Ergebnis still: **0,31 % / 0,03 %**.
  Mit Schwenk 4,6 % (Schnee am Bildrand, dazu die Umbruch-Naht der Simulation). Scrollen danach
  ≤ 1,9 % Abweichung vom gedächtnislosen Vergleich – sichtbar nur kleine Flecken.
- **Onboard-Grafik:** Der Entwicklungsrechner (i7-12700KF) hat keine – „F“ = ohne iGPU. Budget
  weiter nur hochgerechnet (+0,094 ms RTX ≈ 0,9 ms Onboard); echtes Gerät nötig.

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
- HLSL-Dateien in `ZoomIN.Rendering/Shaders/`. Seit M14 Schritt 4 übersetzt sie der Build mit
  `tools/ZoomIN.ShaderCompiler` (derselbe d3dcompiler_47 aus Windows über Vortice, `O3`, also
  ebenfalls kein SDK-Tool in Build oder CI). Eingebettet wird nur der Bytecode
  (`ZoomIN.Shaders.<Datei>.<Einstieg>.<Profil>.cso`).
  - Grund: Die Übersetzung zur Laufzeit kostete etwa 570 ms CPU je Monitor und Pipeline-Start,
    also auch nach jeder Anzeigeänderung. Allein `PhotoFill.hlsl` brauchte 347 ms und
    `Magnify.hlsl` 125 ms (2026-11-07, i7-12700KF).
  - Jeder Einstieg braucht einen `ShaderEntry` in `ZoomIN.Rendering.csproj`.
    `ShaderBytecodeTests` prüft, dass jedes `ShaderSource.Load` im Code eingebettet ist.
  - `#include "X.hlsli"` setzt das Werkzeug selbst ein (`IncludeExpander`, M14 Schritt 10), mit
    `#line`-Marken, damit Fehlermeldungen Datei und Zeile der `.hlsli` nennen. Nur Dateien im
    Ordner `Shaders/`, jede einmal je Shader. Alle `*.hlsli` stehen in den `Inputs` des Targets;
    eine Änderung übersetzt neu. Beispiel: `SmartWeight.hlsli` (randtreues Gewicht der
    Smart-Invertierung), eingebunden von `Magnify.hlsl` nach dessen Ressourcen.
  - In Design-Time-Builds (VS, Rider) übersetzt das Target nicht.
  - Fehlt einer, schlägt der Pipeline-Start mit Log 3003 fehl. Es gibt bewusst keinen Rückfall
    auf Laufzeit-Übersetzung.
  - MSBuild-Falle: `IntermediateOutputPath` steht erst nach dem Projektkörper fest. Ein
    Ausgabepfad daraus muss in einem Target gesetzt werden, sonst landen die `.cso` im
    Quellordner `Shaders/`.
  - Das Werkzeug ist eine `ProjectReference` mit `ReferenceOutputAssembly="false"`,
    `Private="false"` und `OutputItemType`. Ohne `Private="false"` landet es in der
    Veröffentlichung der App.
- Shader werden auf WARP getestet (`tests/ZoomIN.Rendering.Tests`).
- Max. 300 Zeilen pro Shader-Datei; Gemeinsames in `*.hlsli`.
