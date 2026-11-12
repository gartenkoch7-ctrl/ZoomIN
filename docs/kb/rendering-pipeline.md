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
  Während des Neuaufbaus können andere Threads (Zoom-Befehle, Fenster-Ziehen G1, Übersicht B2)
  noch die alte Rendererliste wecken: `RenderWakeup` macht `Wake` und `Sleep` nach `Dispose`
  wirkungslos statt `ObjectDisposedException` (gefunden in M17, Test `RenderWakeupTests`).
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
  aus `Palette`, sonst Monitor-Matrix. Gilt auch beim Zoom, weil im Quellraum geprüft.
  Palette seit M18: **14 feste Einträge** (`WindowPalette.Count`, `PALETTE_SIZE`): 0 Monitor,
  1 Original, 2–9 die eingebauten Filter (Enum-Reihenfolge, 9 = intelligent), 10–13 die vier Plätze
  der eigenen Schemata (`FilterId.Custom1–4`). Ein Index bedeutet immer dasselbe, auch wenn ein Platz
  leer ist (leerer Platz = Matrix „Invertiert“, wie ein gelöschtes Schema).

## Eigene Farbschemata (M18, C1–C3)
- Die Art eines Schemas ist eine **Matrix** wie jeder Filter (`SchemeMatrices`, Herleitung in
  [farbschemata.md](farbschemata.md)); Kontrast/Helligkeit kommen wie gewohnt dazu.
- **Farbe ersetzen** ist keine Matrix: Schritt `ReplaceColors` (`Shaders/ColorReplace.hlsli`)
  **nach der Abtastung, vor `Apply(...)`** und vor der intelligenten Gewichtung. Je Schema bis zu
  3 Ersetzungen; Abstand in YCbCr (Rec. 709), Gewicht `w = 1 − smoothstep(r/2, r, d)`, Pixel wird um
  `(neu − alt)·w` verschoben (Kanten und Schattierungen wandern mit). Radius `r = 0,01 + 0,5·Toleranz`.
  CPU-Referenz mit derselben Formel: `ReplacementTable.Apply` (Tests vergleichen beide).
- **Constant Buffer `b1`** (3,2 KB statt 2,5 KB): nach der Palette `ReplaceCounts[4]` (uint4, x = Anzahl
  je Platz), `ReplaceFrom[12]` (YCbCr + Radius), `ReplaceShift[12]` (RGB). Im Kopf `MonitorScheme`
  (Platz + 1 des Monitorfilters, 0 = keiner) und `ReplaceActive` – 1 nur, wenn auf diesem Monitor der Monitorfilter oder ein Bereich Ersetzungen nutzt; **0 überspringt den Schritt ganz**.
  Befüllt von `WindowConstants.Fill` aus `DisplayEffects.Replacements` (beim Settings-Wechsel gebaut, 0 B je Frame).
- **Pausieren:** Ein „Nur ersetzen“-Fenster hat dieselbe Matrix wie der ungefilterte Monitor; `ChangesMonitor`
  vergleicht deshalb auch `DisplayEffects.ReplaceSlotOf(entry)`.
- **Gemessen (RTX 3060 Ti, 1080p, 2026-11-11):** Magnify 0,081 ms, mit 3 Ersetzungen auf dem ganzen
  Monitor 0,089 ms → **+0,008 ms**, hochgerechnet ×10 ≈ 0,08 ms Onboard; Gesamtframe ≈ 0,9 ms ≤ 2 ms.
  Die Palette mit 14 statt 10 Einträgen ändert die Bereichsschleife nicht (64 Bereiche wie bisher).
  Messung: `ZOOMIN_GPU_TIMING=1 dotnet test --project tests/ZoomIN.Rendering.Tests --filter-class "*SchemeGpuTimeTests"`.
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

## Standbild (M19 Schritt 0)
- `Rendering/Still/StillFrame` kopiert auf Anforderung die Desktop-Kopie eines Monitors
  (`DesktopDuplicator.Texture`) oder einen Teil davon in eine eigene Textur und hält sie bis
  `Release`. Für eingefrorene Monitore (G2) und Inseln (B1) mit „Standbild“.
- Kopie per rohem `CopyResource` bzw. `CopySubresourceRegion` (`DxgiInterop`, Slots 47/46), weil
  Vorticies Wrapper pro Aufruf allozieren. Gleiche Größe → Textur wiederverwendet, `0 B`
  (`StillFrameTests`); neue Größe oder erstes Bild → neue Textur (nur auf Anforderung).
- Nur `B8G8R8A8_UNorm` ohne Multisampling (Format der Desktop-Kopie). Ein 4K-Bild hält ~33 MB
  GPU-Speicher; Zähler `zoomin.render.still_images`.
- Gehört wie das Gerät einem Render-Thread. Verdrahtung in `RenderSession`/`DesktopLayer` machen
  die Spuren G2/B1.

## Inseln (M19 B1)
Plan: [b1-inseln.md](../plan/features/b1-inseln.md). Code: `Core/Islands/`, `Rendering/Islands/`,
`App/Islands/`, `Platform/Input/IslandEditCapture`.
- **Was:** bis zu 4 feste Quellbereiche eines Monitors, vergrößert an fester Stelle desselben
  Monitors über dem Zoom. Ziel = Quelle × Stufe; beim Anlegen wächst das Bild um die Mitte der
  Quelle und wird an den Rändern auf den Monitor geschoben (Uhr in der Ecke bleibt in der Ecke).
  `IslandRules.Fit` hält Quelle und Bild im Monitor, Mindestgröße 16 px, Stufe 1×–16× und so klein,
  dass das Bild passt.
- **Wann gezeichnet** (`OverlayDemand.Islands`, je Frame lock-frei, 0 B): im Vollbild, solange der
  Monitor vergrößert ist (> 1,01×), sichtbar (`Strg+Alt+I`) und eine Insel auf ihm liegt. Lupe und
  Angedockt zeigen keine Inseln (Vorschlag des Plans übernommen). Übersicht und Fenster-Verschieben
  (vorübergehend 1×) blenden sie damit aus. Im Bearbeitungsmodus zeichnen **alle** Monitore, auch
  bei 1×; dann ist `FrameRequest.OverlayOnly` falsch, damit die Desktop-Kopie läuft.
- **Eingefrorener Monitor (G2):** Seine Inseln **bleiben sichtbar und frieren mit ein** (Plan B1,
  Merge-Prüfung). Der Freeze-Controller legt die Inselszene des Augenblicks in `FrozenMonitor.Islands`
  ab (unveränderlich, ohne Bearbeitungsansicht). Je Frame liest `MonitorRenderer` einmal
  `OverlayDemand.Mode(frozen)` und `OverlayDemand.SceneFor(frozen)` und fragt damit
  `Islands(zoom, mode, scene)`. Ein eingefrorener Monitor nutzt so Szene und Modus des Einfrierens
  bis zum Auftauen, auch nach einem Neuaufbau der Pipeline. Log 1720 und der KI-Debug-Bericht nennen
  die Zahl (`FrozenMonitor.ShownIslands`). Ausblenden
  (`Strg+Alt+I`), Einstellungsseite, Profilwechsel und Bearbeiten ändern ihn also erst danach.
  Standbilder entfernter Inseln gibt `MonitorRenderer` auf jedem Frame und vor jedem Schlaf ohne
  Inseln frei (`PruneIslands`), ein Monitor mit Inseln beim Zeichnen, beide gegen die Liste aus
  `SceneFor` (eingefroren: die eingefrorene). Standbild-Inseln auf ihm kopiert
  `IslandStills` aus derselben Textur wie das Hauptbild (`FrozenLayer.DesktopTexture`). `IslandLayer.Draw` tastet dasselbe Bild ab wie das
  Hauptbild (`FrozenLayer.Desktop`), mit Standbild also das Standbild. Solange es gehalten wird,
  liest der Monitor keinen neuen Capture-Frame, und Standbild-Inseln passen dazu. Beim Einfrieren
  mit „live“ bleiben auch die Inseln live. Die vorübergehende 1× des Bearbeitens übergeht ein
  eingefrorener Monitor: Er behält Stufe, Modus, Zaun und Inseln, ohne Bearbeitungsrahmen. Taut er
  während des Bearbeitens auf, zeigt er 1× und macht mit. Einfrieren während des Bearbeitens lehnt
  G2 ab (`island_edit`).
- **Reihenfolge im Frame:** Desktop → Hervorhebungen (Fokus, Caret) → **Inseln** → Übersichtsrahmen
  → Zeiger. Der Zeiger liegt also immer oben. Alles im vorhandenen Overlay-Fenster
  (`WDA_EXCLUDEFROMCAPTURE`), kein neues Fenster.
- **Ein Draw je Insel** mit dem vorhandenen `MagnifyPass` (`MagnifyConstants.ForIsland`): Quelle,
  Bild und 3-px-Rahmen in der Lupenrahmen-Farbe wie bei der Lupe (`WithFrame`). Farbmatrix, Filter
  pro Fenster (Puffer b1 gilt weiter, seine Bereiche liegen in Desktop-UV), eigene Farbschemata,
  intelligentes Invertieren (Gewichte des Frames), Glättung und Schärfe/Fett (`At(Stufe der Insel)`)
  wirken also wie im Hauptbild. Konstanten werden je Draw hochgeladen (144 B); bei ruhigem Bild wird
  ohnehin nichts gezeichnet (M14).
- **Standbild je Insel** (`IslandStills`, 4 Plätze à `StillFrame`): einmal kopiert, sobald die
  Insel als Standbild **zum ersten Mal gezeichnet** wird (nach dem Umschalten, nach einem Neustart
  oder Neuaufbau der Pipeline, z. B. Anzeigewechsel – das Bild wird nie gespeichert) und wenn ihre
  Quelle sich ändert; frei, wenn sie weg oder wieder live ist. Jedes Umschalten auf „Standbild“ zählt `Island.Take` hoch und
  erzwingt ein neues Bild, auch ohne gezeichneten Frame dazwischen. Freigegeben wird erst beim nächsten
  Zeichnen auf dem Monitor. Wer bei 1× auf „Standbild“ schaltet,
  bekommt also das Bild beim nächsten Hineinzoomen. Während des Bearbeitens wird eine sich ändernde
  Quelle (Ecke ziehen) nicht je Schritt neu kopiert (neue Textur und Log je Frame); die Insel zeigt
  dann live und wird nach dem Ende neu aufgenommen. Eine fehlgeschlagene Kopie (Format) wird nicht
  je Frame wiederholt.
  - Die Kopie hält nur den Quellbereich (ganze Pixel). Damit Filter pro Fenster, Gewichte und
    Texelraster weiter in **Desktop-UV** rechnen, bildet `Magnify.hlsl` erst beim Abtasten auf die
    Textur ab: `TexUv(uv) = uv · StillScale + StillOffset` (Konstanten `StillOffset/StillScale`,
    Puffer b0 wächst von 128 auf 144 B). Live: Skala 1, Versatz 0 → jede Koordinate bitgleich wie
    vorher. Auch `SmartWeight.hlsli` und `TextEnhance.hlsli` tasten über `TexUv` ab (WARP-Test:
    Schärfe/Fett auf Standbild = live, innen).
  - Grenze: Die Gewichte des intelligenten Invertierens und die Bereiche des Filters pro Fenster
    kommen vom Live-Bild (ein über die Stelle geschobenes gefiltertes Fenster färbt das Standbild mit). Hat sich der Inhalt
    seit dem Standbild stark geändert, kann die Invertierung im Standbild an Stellen leicht
    abweichen.
- **Bearbeitungsmodus:** Rahmen blinken (Amber/Weiß, alle 0,5 s; `IslandLayer.NeedsRedraw` weckt
  nur beim Wechsel), Griffe (10 px gefüllt) an den Ecken der gewählten Insel, beim Aufziehen ein
  gestricheltes Rechteck (weiße Striche auf dunkler Linie, höchstens 64 Striche je Seite) über den
  Rechteck-Shader des Zeigerdurchgangs.
- **Monitor geändert:** Insel außerhalb ihres Monitors oder auf einem fehlenden Monitor wird nicht
  gezeichnet (nie Absturz), ebenso eine Stufe außerhalb 1×–16× (von Hand geänderte Datei). Beim
  Start von ZoomIN und beim nächsten Bearbeiten schiebt `Fit` sie zurück in den Monitor – nur für die Anzeige;
  gespeichert bleiben die alten Rechtecke, bis der Nutzer eine Insel ändert (ein Beamer für eine
  Weile verkleinert sie nicht dauerhaft). Nach einem
  Anzeigewechsel im laufenden Betrieb bleibt sie bis dahin ausgeblendet (offen: kein Monitor-Ereignis
  in Core).
- **Kosten:** CPU je Frame 2,5 ns, 0 B (`IslandBenchmarks`); `IslandLayer.Draw` mit 4 Inseln,
  Standbild und Bearbeitungsmarken 0 B (`IslandRenderPathTests`). GPU (`IslandGpuTimeTests`,
  `ZOOMIN_GPU_TIMING=1`, 1080p bei 4×, 4 Inseln 600 × 160, RTX 3060 Ti): +0,014 ms schlicht,
  +0,008 ms mit intelligentem Invertieren, +0,018 ms mit Schärfe/Fett (Hauptbild 0,08–0,10 ms).
  ×10 Onboard **hochgerechnet** ≈ 0,2 ms. Im Betrieb `zoomin.render.gpu_time` mit `zoomin.pass=islands`.

## Schärfen und Fett (M18, D1)
Plan: [d1-schaerfen-fetter.md](../plan/features/d1-schaerfen-fetter.md). Code: `Shaders/TextEnhance.hlsli`,
`Magnify.hlsl` `PSEnhanced`, `Core/Filters/TextEnhancement.cs`, `Rendering/TextEnhance/`.

### Spike (2026-11-11, Branch `spike/d1-schaerfen-fetter`, nicht gemergt)
- **Verglichen:** (A) im vorhandenen Durchgang (`PSEnhanced`, auf der Quelle vor Farbmatrix und
  intelligentem Invertieren) und (B) eigener Durchgang auf einem Offscreen-Ziel in Monitorgröße.
- **GPU-Zeit** 1080p, RTX 3060 Ti, Schärfe 60 %/Fett 1 px: A erste Fassung (Unschärfemaske + 20
  eigene Abtastungen) +0,14 ms ✘; **A Runde 2** (9 Bikubik-Abtastungen + 4 für Fett) **+0,026–0,029
  ms** ✔; B +0,146–0,155 ms ✘ (allein Schreiben/Lesen des Offscreen-Ziels 0,016–0,020 ms).
  Faustwert: eine zusätzliche bilineare Abtastung je Pixel kostet bei 1080p ≈ 7 µs.
- B verbreiterte die Nähte des intelligenten Invertierens (helle Kante an der dunklen Zeilenleiste):
  Enhance gehört **vor** das Invertieren. → **Entscheidung: Variante A**, `DesktopLayer` bleibt ein
  Durchgang, kein Offscreen-Ziel.

### Umsetzung (2026-11-12)
- `PSEnhanced` wird nur gebunden, wenn `TextEnhancement.At(Zoom, Glättung)` etwas liefert: Schärfe
  oder Fett > 0, Glättung „Text-optimiert“ und Zoom ≥ 1,5×. Sonst läuft `PSMain` unverändert
  (0 Zusatzkosten, bitgleich). Die Werte stehen im Konstantenpuffer b0 (`MagnifyConstants`, 128 B).
  Nur der vergrößerte Bereich wird verbessert; die 1×-Fläche um Lupe/angedockten Bereich nicht.
  Als Zoom zählt die weniger vergrößerte Achse der gezeichneten Ansicht (angedockt können sich die
  Achsen stark unterscheiden, z. B. 2,3× quer und 1,2× hoch → aus).
- **Schärfe = Catmull-Rom an umgerechneter Position.** Zwischen zwei Texeln wird die Mitte
  schneller durchlaufen, an den Texelmitten entstehen kleine Plateaus (bei 100 % ein Drittel Texel
  Übergang). Gleiche 9 Abtastungen, kostet nichts. *Abweichung vom Spike:* Der dort genutzte
  schärfere Keys-Kern (`a = −0,5 − Schärfe`) macht Kanten **nicht** steiler – seine Steigung an
  einer Kante ist `1,5 + a/2` (Catmull-Rom 1,25, `a = −1,5` nur 0,75); er wirkte nur über
  Überschwinger und dunklere Striche. Nachgewiesen im WARP-Test (Kante gleich breit).
- **Keine Begrenzung auf den Wertebereich der Abtastungen:** Die mittleren Abtastungen sind
  Mischungen zweier Texel, ihr Bereich ist enger als der echte – die Begrenzung schnitt echte
  Catmull-Rom-Werte ab und machte jede Kante weicher (Strichmitte 65 statt 62). Ohne sie bleibt das
  leichte Überschwingen von Catmull-Rom wie heute; die Umrechnung erzeugt kein neues. Fett zieht
  helle Überschwinger am Papier sogar auf den Papierton zurück.
- **Fett = Minimum/Maximum der Leuchtdichte** über ein Kreuz aus 4 bilinearen Abtastungen im
  Abstand „Fett px“ (Ausgabepixel, aus `ddx/ddy` der Uv – exakt, weil Uv linear ist). Ob der Strich
  dunkel oder hell ist, entscheidet die Umgebung (Mittel der 9 Abtastungen gegen die Mitte des
  Bereichs) – gleich vor und nach jedem invertierenden Filter.
  - Kontrast-Prüfung über 9 Abtastungen **und** Kreuz zusammen: Die 9 allein übersehen einen
    Strich ein Texel weiter (der Bilinear-Trick mischt ihn mit kleinem Gewicht ein, es entstand
    eine Lücke im verbreiterten Strich), das Kreuz allein bei kleinem Fett-Wert.
  - **Reichweite höchstens ½ Texel** (`BOLD_MAX_TEXELS`): Weiter sprangen die 4 Abtastungen über
    den Rand eines 1-Texel-Strichs, und neben einer helleren Lücke erschien eine Geisterkopie
    (gesehen bei 2× mit 3 px). Bei 2× wirkt Fett also höchstens 1 px, bei 4× 2 px, ab 6× voll.
  - Vor dem Skalieren wird der Catmull-Rom-Wert auf 0–1 begrenzt: Ein Überschwinger über Weiß
    (zwischen zwei weißen Texeln 1,125) teilte sonst durch `1 − 1,125` und machte helle Schrift
    auf dunklem Grund fleckig schwarz (WARP-Test `Bold_WhiteStrokeOnBlack…`).
  - **Grenze:** Ist ein Strich so breit wie die halbe Umgebung (2 Texel bei 4 × 4), ist hell und
    dunkel gleich vertreten; dann gewinnt „dunkel“. Dunkle Schrift wird dort richtig breiter, helle
    Schrift auf dunklem Grund an diesen Stellen etwas schmaler. Gilt nur für native dunkle
    Oberflächen; mit Invertier-Filter ist die Quelle hell und alles stimmt.
- **Risiko 1, ClearType-Säume** *(selbst entschieden, Vorgabe Nutzer)*: Nur die Leuchtdichte des
  dunkelsten/hellsten Werts wird übernommen; der Pixel wird zu Schwarz bzw. Weiß hin skaliert und
  behält seinen Farbton, die Sättigung kann nur sinken. Test: Fett erzeugt keine zusätzlichen
  farbigen Pixel. Folge: Der verbreiterte Rand farbiger Schrift wird grau-dunkel, nicht farbig.
- **Risiko 2, Fotos** *(selbst entschieden)*: Die 4 Eck-Abtastungen des Bikubik-Fensters (4 × 4
  Texel, liegen immer auf Texelmitten) werden zuerst gelesen. Sind 3–4 davon farbig (Chroma-Schwellen
  wie `Ambient.hlsl`, 0,10–0,25), gilt der Pixel als Foto: **Schärfe und Fett sind dort aus**
  (`smoothstep(2, 3, farbige Ecken)`: bis 2 voll, ab 3 aus). Ohne Zusatz-Abtastung. Bekannte Grenze: Text auf kräftig
  farbiger Fläche (blaue Schaltfläche) wird ebenfalls nicht verbessert; graue Fotos werden verbessert.
- **Risiko 3, Glättung** *(selbst entschieden)*: Nur bei „Text-optimiert“. Bei „Weich“/„Pixel“
  sind die Regler deaktiviert, die Beschreibung sagt „Wirkt nur mit der Glättung ‚Text-optimiert‘“;
  kein stilles Umschalten. Die Werte bleiben gespeichert und wirken wieder, sobald „Text-optimiert“
  gewählt ist.
- **Risiko 4, unter 1,5×** *(selbst entschieden)*: aus (`TextEnhancement.MinZoom`).
- **Sichtbare Eigenheiten** (WARP-Bögen mit echter 9-pt-ClearType-Schrift): Schärfe 100 % zeigt die
  Texel des Quellbilds deutlicher, auch ClearType-Farbpixel als kleine farbige Flecken (wie
  „Pixel“, nur kleiner). Fett 3 px bei 4× füllt kleine Punzen (e, a). Bewertung durch den Nutzer offen.
- **GPU-Zeit** (`EnhanceGpuTimeTests`, `ZOOMIN_GPU_TIMING=1`, 1080p, 5 Läufe, RTX 3060 Ti):
  +0,018–0,030 ms bei 60 %/1 px und 100 %/3 px, 2×/4×/8×, ohne und mit intelligentem Invertieren
  (Magnify aus 0,071–0,090 ms). ×10 Onboard **hochgerechnet** ≈ 0,3 ms ≤ 0,5 ms. Im Betrieb misst
  `EnhanceCost` dasselbe nach jeder Änderung und nach einer Pause (`zoomin.render.enhance_gpu_ms`),
  nur mit Zuhörer, auf genau der gezeichneten Ansicht (bei Lupe nur deren Fläche).
- Der Debug-Schnappschuss (`-info.txt`) nennt Glättung, Schärfe, Fett und ob sie bei diesem Zoom wirken.


## Eingefrorener Monitor (M19 G2)
- `ZoomModel.Frozen` (`MonitorFreeze`) hält einen unveränderlichen `FreezeState`; der Render-Thread
  liest je Frame `Frozen.For(monitor)` lock-frei, `0 B` (Benchmark `FreezeBenchmarks`).
- `MonitorRenderer`: eingefroren → Zielstufe ist die Stufe des Augenblicks (`FrozenMonitor.Level`),
  Modus, Lupe und Dock ebenso; vorübergehende Stufen und die Übersicht wirken nicht. Der Renderer
  schläft nicht, auch bei 1× nicht (Rahmen).
- `RenderSession`: der Anker wird nicht weitergeschoben (`AnchorController.Next` läuft nicht), es
  gilt der Anker des letzten Live-Frames. Er wird im `FrozenMonitor` abgelegt (einmal, atomar), so
  zeigt eine nach einem Anzeigewechsel neu gebaute Pipeline denselben Ausschnitt; nach dem Aufheben
  beginnt ihr `AnchorController` dort (`StartAt`) statt in der Monitormitte. Zeiger,
  Fokusrahmen und Caret werden dort nicht gezeichnet (`_cursorInside` ist falsch).
- `Rendering/Freeze/FrozenLayer`: Standbild über den Baustein `StillFrame` – beim ersten Frame des
  Einfrierens aus `DesktopDuplicator.Texture`, danach liest die Sitzung **keinen** Capture-Frame
  mehr (`HoldsStill`). Ein neues Einfrieren (neues Objekt) nimmt ein neues Bild, Aufheben gibt die
  Textur frei. Bei Standbild wird auch bei 1× der Desktop gezeichnet (`FrameRequest.OverlayOnly`),
  sonst schiene der Live-Desktop durch. Fehlt das Bild, bleibt der Monitor live (Log 3312).
  Das Standbild lebt im Gerät der Pipeline: Ein Anzeigewechsel hebt Standbild-Einfrieren deshalb
  auf (Controller). Nach einem Neustart der Pipeline wegen eines Fehlers (ohne Anzeigewechsel) nimmt
  die neue Sitzung kein neues Bild (`FrozenMonitor.ClaimStill`: nur einmal je Einfrieren), sondern
  zeigt live (Log 3312) – ein späteres Bild als Standbild auszugeben wäre irreführender.
  `RenderSession.Hide` gibt ein Standbild sofort frei (ein aufgetauter Monitor bei 1× schläft).
  Genommen wird es nur aus einer aktuellen Desktop-Kopie (`DesktopDuplicator.IsCurrent`): nach
  `ACCESS_LOST` (Win+L, UAC) wartet es auf den nächsten Frame, statt ein altes Bild festzuhalten.
  Steht der Zeiger doch auf einem eingefrorenen Monitor, zeigt Windows seinen eigenen Zeiger
  (`FrozenLayer.NeedsSystemPointer`, auch einmal direkt nach dem Sprung). In Lupe und Angedockt
  friert das Standbild nur den vergrößerten Ausschnitt ein; daneben bleibt der Desktop sichtbar
  wie immer (gewollt: dort ist ZoomIN durchsichtig). Ein eingefrorener Monitor bei 1× „Live“ läuft im
  Leerlauf-Takt (eine Prüfung je Bildwechsel), damit der Rahmen stehen bleibt.
  Mit dem Standbild bleiben auch die Fensterbereiche des Filters pro Fenster stehen, sonst lägen die
  Filter auf Fenstern, die im Bild gar nicht mehr dort sind.
- **Rahmen:** 6 px innen am Monitorrand in der Einstellungsfarbe (Werk: Cyan), gezeichnet mit dem
  Rechteck-Shader des `CursorPass` wie der Fokusrahmen – nicht im `Magnify.hlsl`, denn dessen
  Rahmen umgibt nur Lupe/Streifen, und `DesktopLayer`/`Magnify.hlsl` gehören Spur B1.
  `WDA_EXCLUDEFROMCAPTURE` gilt unverändert fürs Overlay.
- WARP-Tests `FrozenLayerTests`: Anker bleibt, Live-Inhalt ändert sich, Standbild nicht; `0 B`.

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
