# Eingabe & Tracking

## Problem: Klicks im vergrößerten Bild
Das Overlay ist durchklickbar – Klicks landen am **echten** Mauspunkt auf dem Desktop.
Damit das zum Bild passt, muss der echte Mauspunkt immer dort angezeigt werden, wo er
tatsächlich ist.

## MVP-Lösung: Cursor-Fixpunkt (ADR-0003)
Vergrößert wird um den Mauszeiger `p` als Fixpunkt:

```
angezeigt(x) = p + z · (x − p)      ⇒   angezeigt(p) = p
```
- Der Punkt unter der Maus stimmt immer → Klicks treffen ohne Sonderrechte.
- Bewegt sich die Maus an den Rand, wandert der Ausschnitt proportional mit; der gesamte
  Desktop bleibt erreichbar.
- Funktioniert ohne UIAccess, ohne Signatur, ohne Installation in `Program Files`.

## Modi und Klicks (M4)
| Modus | Geometrie | Klicks |
|---|---|---|
| Vollbild | ganzer Monitor, Fixpunkt Maus | treffen |
| Lupe | Rechteck um die Maus (im Monitor gehalten), Quelle = p + (Ziel − p) / z | treffen (Fixpunkt) |
| Angedockt | reservierter Streifen zeigt Umgebung der Maus aus dem Restbereich | im Streifen: reine Anzeige |

Rechnung in `Core/Modes/ModeGeometry.cs`, Beweis per Test (`Lens_CursorPixel_IsDisplayedAtCursor`).

## Tracking durch Fokus/Caret
Springt der Viewport zum Tastaturfokus/Caret, stimmen Bild und Mausposition nicht mehr
überein. MVP-Verhalten: Beim Fokus-Tracking wird die Maus **nicht** bewegt; sobald die Maus
bewegt wird, übernimmt wieder der Cursor-Fixpunkt. ⚠️ Mit Nutzern testen.

## Mauszeiger in der Mitte (ADR-0018)
Modus `MouseFollowMode.Center` (bis 1.0 der Schalter `CenterMouse`, Werkswert; seit M17 ist
„Randabstand“ der Werkswert, ADR-0018 gilt weiter): `AnchorController` legt den
Fixpunkt mit `ViewCentering.AnchorFor` so, dass die Maus in der **Bildmitte** angezeigt wird
(Vollbild, Lupe). Am Monitorrand bleibt die Ansicht stehen und der Zeiger rückt zum Rand.
Angedockt ist unverändert. Klicks treffen trotzdem: Der Systemzeiger ist beim Zoomen
ausgeblendet (ADR-0011), ZoomIN zeichnet ihn dort, wo sein Desktop-Punkt angezeigt wird, und der
Klick landet genau an diesem Desktop-Punkt. Ein Input-Transform ist dafür **nicht** nötig.
- Folge: Pro Mauspixel wandert der Inhalt um `z` Bildschirmpixel (der Zeiger steht still).
- Tests: `MouseCenteringTests` (Mitte, Klickpunkt unter dem Zeiger über den ganzen Monitor,
  Monitorrand, Zoomwechsel, Rückkehr vom Fokus). Benchmark `AnchorCenterMouse` 19 ns, 0 B.
- Modus „Fixpunkt“ = Fixpunkt an der Maus (ADR-0003).

## Randabstand (Werkswert seit M17, Feature A1, ADR-0029)
`TrackingOptions.MouseFollow = Margin`, Abstand `MouseMargin` 0–0,5 (Werk 0,2), Seite
„Fokus & Textcursor“ → „Maus folgen“. Rechnung in `Core/Tracking/MouseFollowTarget`.
- Liegt die Maus im inneren Bereich des sichtbaren Ausschnitts (Ausschnitt minus Abstand je
  Seite), bleibt der Anker **bitgenau** stehen – das Bild ruht, der gezeichnete Zeiger wandert.
- Sonst wird die Ausschnittsmitte um die kleinste Verschiebung bewegt, die die Maus genau auf den
  Abstand bringt, und mit `ViewCentering.AnchorFor` in einen Fixpunkt umgerechnet – wie beim
  Caret mit `CaretMargin`. Kein Verzögern, kein Springen: am Rand läuft das Bild 1:1 mit.
- Pro Achse: Eine Achse ohne Schub behält ihren Ankerwert exakt. Schübe ≤ 1e-6 px gelten als
  Rundung. Sonst würde der Rückweg Ausschnitt → Anker jedes Frame um Bruchteile eines Pixels
  wackeln, und der Render-Loop käme am Monitorrand nie in den Leerlauf.
- Monitorrand: `AnchorFor` klemmt, die Ansicht steht, der Zeiger rückt in den Rand (kein Zittern,
  Test `AtTheMonitorEdge_TheViewStops_WithoutJitter`).
- Zoomwechsel (Vollbild; die Lupe bleibt stehen, sonst wanderte ihr Fenster): Der Anker wird so umgerechnet, dass der Zeiger auf dem Bildschirm stehen bleibt
  (Vollbild und Lupe zeigen `x` bei `p + z·(x − p)`, also `p' = (z'·m − s) / (z' − 1)`). Ab 1×
  ergibt das die Maus selbst: Hineinzoomen beginnt am Zeiger wie beim Fixpunkt.
- Moduswechsel (z. B. Vollbild → Lupe): Der Anker startet an der Maus, die Lupe öffnet also am
  Zeiger; der alte Anker gehört zu einer anderen Geometrie.
- Messwert `zoomin.tracking.mouse_push` zählt nur Mausschritte (nicht angedockt, nicht bei 1×,
  nicht während sich der Zoom ändert).
- Rückkehr von Fokus/Caret (Maus > 8 px bewegt): Der Randabstand rechnet vom aktuellen Ziel aus,
  liegt die Maus im Bild, bewegt sich nichts.
- 0,5 = kein Innenbereich, der Zeiger bleibt in der Mitte (wie „Mitte“); 0 = erst ganz am Rand.
- Lupe: gleiche Regel auf dem kleinen Quellausschnitt der Lupe; die Lupe steht still, solange der
  Zeiger innen bleibt. Angedockt: unverändert (Umgebung der Maus).
- Klicks: Der Zeiger wird an seinem angezeigten Desktop-Punkt gezeichnet (ADR-0018). Test
  `MouseMarginClickTests` fährt den ganzen Monitor ab, auch mit Zoomwechseln und Sprüngen.
- `ViewPlacement` in `AnchorInput` (für M17 B2 „hineinzoomen am Rahmen“): setzt den Maus-Anker so,
  dass ein Punkt bei der Zielzoomstufe mittig steht; bis der Zoom dort ankommt, wird nicht auf
  den Zeiger umgerechnet. Danach gilt der Randabstand. In „Mitte“/„Fixpunkt“ übernimmt die Maus
  sofort wieder.
- Migration: Einstellungen mit `SchemaVersion` 1 werden beim ersten Start **einmal** auf
  Randabstand gestellt (`SettingsMigration`, Log 1500), sofort gespeichert und der Hinweis
  (Windows-Benachrichtigung, Klick öffnet die Seite, Log 1502) einmal gezeigt. `CenterMouse` wird
  nur noch gelesen. Profile aus 1.0 haben kein `MouseFollow` und bekommen beim Laden den
  Werkswert Randabstand.
- Tray-Hinweise (gelernt mit A1): Windows zeigt einen Ballon je Symbol, ein neuer ersetzt den
  angezeigten sofort, und ein Klick meldet nur „Ballon geklickt“ (NIN_BALLOONUSERCLICK), ohne zu
  sagen, welcher. Der Klick gehört darum zum zuletzt gezeigten Hinweis
  (`ITrayIcon.Notify(…, clicked)`). Hinweise laufen über den Tray-Thread, damit sie nach dem
  Anlegen des Symbols kommen (direkt nach dem Start schlug NIM_MODIFY sonst stumm fehl); ohne
  Symbol warten sie, bis es (wieder) angelegt ist. Beim
  Start mit dem Randabstand-Hinweis entfällt die UIAccess-Benachrichtigung einmal.
- Benchmark `AnchorMargin` siehe `performance.md`.

## Später: Input-Transform
`MagSetInputTransform` bildet Eingaben aufs vergrößerte Bild ab und erlaubt weitere
Funktionen wie eine an den Zoom angepasste Mausgeschwindigkeit. Voraussetzungen: `uiAccess="true"` im Manifest,
signierte EXE, Installation in einem geschützten Ordner. Erst mit Installer + Code-Signing.

## Tracking-Quellen
| Quelle | API | Hinweis |
|---|---|---|
| Maus | `GetCursorPos` pro Frame (+ Low-Level-Hook nur für Hotkeys/Gesten) | Polling im Render-Thread ist am latenzärmsten |
| Fokus | UIA `GetFocusedElement` → `CurrentBoundingRectangle`, ~30×/s gepollt | Eigener MTA-Thread, nie im Render-Thread |
| Caret | Kette in `Platform/Automation/FocusReader`, ~30×/s | siehe „Caret-Kette“ |

`Core/Tracking/AnchorController` entscheidet pro Monitor, welche Quelle gewinnt (siehe M6-Plan).

### Caret-Kette (2026-11-05)
Viele Apps zeichnen ihren Textcursor selbst und haben keinen Win32-System-Caret. `FocusReader`
nimmt deshalb die erste Quelle, die antwortet (billigste zuerst):
1. `GuiThreadCaret`: `GetGUIThreadInfo` (Win32, WinForms, klassische Edits).
2. `MsaaCaret`: `AccessibleObjectFromWindow(OBJID_CARET)` → `accLocation`. Unsichtbar
   (`STATE_SYSTEM_INVISIBLE`) zählt nicht. Chromium und Firefox stellen hier einen Caret für Lupen
   bereit, auch ohne System-Caret.
3. `UiaTextCaret`: `TextPattern2.GetCaretRange` (nur wenn `isActive`), sonst das Ende der
   letzten Auswahl aus `TextPattern.GetSelection`. Ein leerer Bereich hat bei vielen Anbietern kein
   Rechteck, dann gilt der linke Rand des folgenden Zeichens (`ExpandToEnclosingUnit(Character)`).
   Gedacht für Word, Windows Terminal, Konsole, WPF/WinUI.
4. Notlösung: Fokus-Element vom Typ **DataItem** (Tabellenzelle, z. B. Excel) oder **Edit**, höchstens
   1600 × 200 px, steht für den Caret (`TrackingSnapshot.CaretEstimated`). Die Ansicht läuft wie
   beim Tippen am Randabstand mit (Pfeiltasten in Excel springen nicht jedes Mal in die Mitte),
   gezeichnet wird aber **kein** Caret-Balken, nur der Fokusrahmen.

**Chromium/Electron** (Chrome, Edge, VS Code, Teams, Discord) baut seinen Barrierefreiheitsbaum
erst, wenn es eine Hilfstechnologie vermutet. `ChromiumAccessibility` erkennt das Fokusfenster
an den Klassen `Chrome_RenderWidgetHostHWND` / `Chrome_WidgetWin_1` und fragt das Inhaltsfenster
einmal per `IServiceProvider.QueryService` nach `IAccessible2`, wie es Bildschirmleser tun. Das
wirkt nur auf diesen Browserprozess. Das systemweite `SPI_SETSCREENREADER` wird bewusst **nicht**
gesetzt: Es ändert das Verhalten aller Apps und müsste nach einem Absturz zurückgesetzt werden
(Entscheidung Nutzer 2026-11-05). ⚠️ Annahme, noch nicht gemessen; Prüfung mit
`tools/ZoomIN.TrackingProbe` (zeigt je App, welche Quelle antwortet; `--no-activate` ohne Anstoß).

**Fehlerantworten fremder Apps (Absturz 2026-11-09):** Ein Chromium-Fenster beantwortete
`QueryService` mit `E_INVALIDARG`. COM-Interop macht daraus eine `ArgumentException`, keine
`COMException` – sie wurde nicht gefangen, beendete ZoomIN, und nach jedem Watchdog-Neustart traf
das Tracking dasselbe Fenster wieder (3 Neustarts, dann Aufgabe). Darum: Fehlerantworten fremder
Apps fängt `ComFailure.IsAnswer` (auch `E_NOINTERFACE`, `E_NOTIMPL`, `E_ACCESSDENIED`), und der
Tracking-Thread fängt zusätzlich jede andere Ausnahme einer Abfrage ab (Log 2003), statt den
Prozess zu beenden.

## Kompatibilität Fokus / Caret (M6)
| Anwendung | Fokus | Caret | Quelle |
|---|---|---|---|
| WinForms/Win32-Textfelder | ✅ | ✅ | automatische Probe 2026-10-30 |
| Notepad (Win 11, 11.2609) | ✅ | ✅ System + MSAA + UIA | `TrackingProbe --auto` 2026-11-05 (3 von 5 Läufen, sonst Notepad-Dialog) |
| Konsole (conhost) | ✅ | ✅ System + UIA (Block 8×16) | `--auto` 2026-11-05 |
| Windows Terminal | ✅ | ✅ **nur UIA** | `--auto` 2026-11-05 – ohne Caret-Kette kein Caret |
| Chrome, Edge (Textfeld) | ✅ | ✅ MSAA + UIA, kein System-Caret | `--auto` 2026-11-05, ⚠️ NVDA lief |
| Word | ✅ | ✅ System + UIA | `--auto` 2026-11-05 |
| Excel | ✅ | ✅ System-Caret = ganze Zelle → als Zelle (ohne Balken) | `--auto` 2026-11-05 |
| Explorer, Einstellungen, Outlook, Firefox | ? | ? | offen |
| VS Code, Teams, Discord | ? | ? | nicht installiert bzw. nicht getestet |

### Automatischer Lauf (`tools/ZoomIN.TrackingProbe --auto [--only Name]`)
Startet jede installierte Matrix-App selbst mit einer Testdatei, in der die App von sich aus einen
Caret zeigt (es wird nichts getippt). Gemessen werden je App: welche Quelle antwortet (vor und nach
dem Chromium-Anstoß), die Abweichung zwischen den Quellen, ob der Caret im App-Fenster liegt und
wie oft der echte Tracking-Thread in 3 s ohne Tippen „Caret bewegt“ meldet (muss 0 sein).
Browser laufen mit eigenem `--user-data-dir`. Geschlossen werden nur Prozesse, die der Lauf
selbst gestartet hat.
- **Notepad ist ausgenommen.** Windows-11-Notepad stellt bei jedem Start die Sitzung mit den Tabs
  des Nutzers wieder her; ein Testlauf arbeitet dann in dessen Dokumenten (Vorfall 2026-11-05).
  Notepad nur von Hand testen.
- Excel startet sein Arbeitsfenster in einem zweiten Prozess; gemessen wird deshalb das Fenster,
  das vorn liegt, wenn es zu einem neu gestarteten Prozess mit dem richtigen Namen gehört.
- Ein laufender Bildschirmleser (NVDA) schaltet die Barrierefreiheit von Chromium selbst ein. Der
  Vergleich vorher/nachher sagt dann nichts über den Anstoß; der Bericht zeigt das Screenreader-Flag.

### Blinkender Caret und Zellen-Caret (`Core/Tracking/CaretMotion`, 2026-11-05)
- Notepad nimmt den System-Caret im Blinktakt weg. Die Kette fällt dann auf UIA zurück, und UIA
  meldet die Zeilenhöhe (420,257 1×34) statt der Caret-Höhe (420,273 1×18), am selben Punkt.
  Bisher galt jedes Wiederauftauchen als neue Caret-Bewegung: Ruhte die Maus 0,5 s, holte der
  blinkende Caret die Ansicht zurück. Jetzt gilt ein Caret nur als bewegt, wenn sich linke Kante
  oder Unterkante um mehr als 1 px ändern (`SamePlace`). Am selben Fleck bleibt die gezeigte Form
  (kein flackernder Balken). Prüfung: 0 Fehlbewegungen in allen Apps.
- Excel meldet die aktive Zelle als System-Caret (81×21). Ein Caret, der breiter als hoch ist
  (`IsCell`), gilt als geschätzt: Die Ansicht folgt, ein Balken wird nicht gezeichnet. Block-Carets
  der Konsole (8×16) sind schmaler und bleiben echte Carets.

## Zeiger-Auslöser (UI-Neugestaltung)
`Platform/Input/Win32InputActivityHook` beobachtet mit eigenem `WH_MOUSE_LL` + `WH_KEYBOARD_LL`
(nur lesen, nichts verschlucken) Mausbewegung und Tippen. `App/Pointer/PointerActivity` macht
daraus die Suchhilfe bei Schütteln (`Core/Pointer/ShakeDetector`: 4 Richtungswechsel à ≥ 40 px
in 0,8 s, Zittern < 8 px zählt nicht), nach ≥ 3 s Ruhe (`IdleDetector`) oder bei Strg allein
(gedrückt und losgelassen ohne andere Taste/Klick; AltGr sendet Strg+Alt und zählt nicht).
„Zeiger beim Tippen ausblenden“: Zeichentaste ohne Strg/Alt/Win, endet mit der nächsten
Mausbewegung. Pro Mausbewegung keine Allokation. Ohne Zoom und Filter zeichnet das Overlay
nur Zeiger/Fadenkreuz/Ring auf transparentem Grund, ohne den Desktop zu kopieren.

## Windows-Zeigereinstellungen überschreiben
Windows reicht über `GetCursorInfo` bereits vergrößerte bzw. eingefärbte Zeiger heraus
(Barrierefreiheit → Mauszeiger: `HKCU\Control Panel\Cursors\CursorBaseSize`, z. B. 112 statt 32;
die erzeugten Dateien liegen in `%LocalAppData%\Microsoft\Windows\Cursors\*_eoa.cur`). Mit
„Überschreiben“ lädt `Platform/Cursor/StandardPointers` stattdessen die unveränderten Originale
aus `%SystemRoot%\Cursors` per `LoadImage(LR_LOADFROMFILE)` in genau der benötigten Pixelgröße
(scharf statt hochskaliert; die aero-Dateien enthalten Bilder bis 128 px).
- Zuordnung Handle → Datei: System-Zeiger sind geteilte Handles, `GetCursorInfo` liefert dasselbe
  Handle wie `LoadCursor(NULL, IDC_*)`. I-Balken und Fadenkreuz haben keine aero-Datei, genutzt
  werden `beam_r.cur`/`cross_r.cur` (monochrom, Invertier-Pixel werden schwarz gezeichnet).
- Anwendungszeiger ohne Standard-Gegenstück: Windows-Bild, nur auf Standardgröße
  (32 px × System-DPI/96) verkleinert; ihre Windows-Farbe bleibt.
- `CursorShape.BaseSize`/`Density`: Bildpixel pro Bildschirmpixel bei Größe 1×. Geladen wird je
  Zeiger in Stufen 1/2/4/8× (`PointerStyle.SharpScale`), damit die Zoom-Animation höchstens vier
  Bilder braucht und keines mehr als halbiert wird (bilinear ohne Mipmaps bleibt glatt).
- Größe: `UnzoomedScale × Zoom`, gedeckelt bei `MaxScale` (nie unter der Größe ohne Zoom);
  „Größe relativ zum Zoom“ aus = immer `UnzoomedScale`. Mit Überschreiben zeichnet ZoomIN den
  Zeiger auch ohne Zoom (Overlay läuft dauerhaft).
- ⚠️ Mehrere Monitore mit unterschiedlicher DPI: Grundgröße folgt der System-DPI, nicht der des
  Monitors – im Sichttest prüfen.

### Zeiger am unteren/rechten Bildschirmrand (2026-11-03)
Die Zeigerspitze (Hotspot) wird exakt am Klickpunkt gezeichnet (ADR-0003). Am unteren oder
rechten Rand hängt das vergrößerte Bild (bei 4× ~128 px) deshalb fast ganz über den Monitor
hinaus – der Zeiger „verschwindet“. Lösung (Entscheidung Nutzer: umklappen statt verschieben):
`Core/Pointer/PointerPlacement` spiegelt das Bild auf der betroffenen Achse, sobald mehr als
1/8 abgeschnitten würde **und** die Spiegelung weniger abschneidet. Die Spitze bleibt am
Klickpunkt, der Pfeil zeigt in den Bildschirm. Hysterese: zurück erst unter 1/16, damit ein
Zeiger genau auf der Grenze nicht flackert. Zeiger mit mittigem Hotspot (I-Balken) klappen nie.
- Gespiegelt wird ohne Shader-Änderung über die Konstanten: Der Shader rechnet
  `uv = (Uv − InnerOffset) / InnerScale`; Offset am fernen Rand + negative Skala ergibt `1 − uv`
  (`CursorConstants.ForPointer(..., flipU, flipV)`, GPU-Test auf WARP in `CursorPassTests`).

## Tastenkürzel
Schema und Standardbelegung: [ADR-0008](../adr/0008-tastenkuerzel-strg-alt.md)
(Strg+Alt+Taste ohne AltGr-Belegung, Strg+Mausrad mit Vorrang vor Anwendungen).
Globale Kürzel per `RegisterHotKey`, Mausrad per `WH_MOUSE_LL`.

## Fokus & Caret – Verhalten (M6)
- Fokus-Schwenk weich (~200 ms) oder sofort, einstellbar. **Jeder** Fokuswechsel wird in die
  Bildmitte gelegt, auch wenn das Element schon sichtbar war (Entscheidung 2026-11-03).
- Caret mit Randabstand (Standard 20 %): Erreicht er den Rand, wird die Ansicht nur so weit
  **geschoben**, dass er genau auf dem Randabstand steht – gleichmäßiges Mitlaufen beim Tippen.
  Ist er ganz außer Sicht (neue Zeile, anderes Feld), wird er zentriert.
- Am Monitorrand bleibt die Ansicht stehen (nie Fläche jenseits des Desktops); der Fokus steht
  dann außermittig, aber sichtbar.
- Mausbewegung über Schwellwert → gleitend zurück zum Maus-Fixpunkt.

### Fixpunkt ≠ Bildmitte (Ursache „Fokus landet am Rand“, behoben 2026-11-03)
Die Geometrie (`ModeGeometry`) vergrößert um einen **Fixpunkt**, und der wird dort gezeigt, wo er
auf dem Desktop liegt (ADR-0003). Bis dahin wurde die Fokusmitte direkt als Fixpunkt benutzt –
ein Element bei x = 100 px erschien vergrößert ebenfalls bei x = 100 px, also am Rand. Beim
Caret kam hinzu: Er stand damit im Randbereich, also schwenkte die Ansicht bei jedem Zeichen
erneut (Springen). Außerdem galt ein Fokus, der gerade noch am Rand sichtbar war, als „sichtbar“.
- Lösung: `Core/Modes/ViewCentering.AnchorFor` rechnet die gewünschte **Bildmitte** in den
  Fixpunkt um (Umkehrung von `ModeGeometry`, je Achse; Vollbild = Lupe über den ganzen
  Monitor; Angedockt zentriert schon selbst). `AnchorController` merkt sich die Bildmitte und
  rechnet den Fixpunkt jeden Frame neu – ein Zoomwechsel hält den Fokus so in der Mitte.
- Tests: `ViewCenteringTests` (Raster über Monitor mit negativem Ursprung, alle Modi, Zoom
  1–16×), `AnchorCenteringTests` (Mitte, Schieben am Rand, Monitorrand ohne Zittern).

### Caret in DPI-unbewussten Fenstern
`GetGUIThreadInfo.rcCaret` liegt in Client-Koordinaten **im DPI-Raum des Caret-Fensters**.
Aus unserem Per-Monitor-v2-Thread umgerechnet, landet der Caret bei DPI-unbewussten oder
System-DPI-Apps auf skalierten Monitoren um den Skalierungsfaktor versetzt (zu weit rechts/
unten, zu klein). `UiaTrackingSource` rechnet deshalb `ClientToScreen` im DPI-Kontext des
Fensters (`GetWindowDpiAwarenessContext` → `SetThreadDpiAwarenessContext`) und danach
`LogicalToPhysicalPointForPerMonitorDPI`. Für Per-Monitor-Apps ändert sich nichts.
⚠️ Nicht automatisch getestet – Sichttest mit einer alten Win32-App bei 150 % Skalierung.
UIA-Rechtecke (`CurrentBoundingRectangle`) sind für einen Per-Monitor-Client bereits physisch.

## Fenster-Verschieben erkennen (G1, Mini-Spike 2026-11-11)
Ziel: Solange ein Fenster gezogen wird, geht der Monitor unter dem Zeiger vorübergehend auf 1×
(Plan [G1](../plan/features/g1-zoom-beim-verschieben.md)). Gemessen mit einem eigenen
Win32-Testfenster (WS_OVERLAPPEDWINDOW) und eigenem Hook-Thread, keine fremde App angefasst.

**Ereignisse:** `EVENT_SYSTEM_MOVESIZESTART/END` (0x000A/0x000B, out-of-context) kommen für
Ziehen mit der Maus und für Alt+Leertaste → Verschieben/Größe ändern. `GetGUIThreadInfo` zeigt
während der Schleife `GUI_INMOVESIZE` und `hwndMoveSize` = Fenster, danach nicht mehr. Ein
`SC_MOVE|HTCAPTION` ohne gedrückte Taste löst gar nichts aus.

**Verschieben oder Größe – Ergebnis:**
| Weg | Trefferzone (`WM_NCHITTEST` am Zeiger beim Start) | Erste Rechteckänderung |
|---|---|---|
| Titelleiste ziehen | 2 = Titel → Verschieben | gleiche Größe |
| Rand/Ecke ziehen | 10–17 → Größe | andere Größe |
| Alt+Leertaste → Verschieben | 2 (Windows setzt den Zeiger auf die Titelleiste) | gleiche Größe |
| Alt+Leertaste → Größe ändern | 1 = Client (Zeiger in der Fenstermitte) → **unklar** | andere Größe |
| Maximiertes Fenster an der Titelleiste ziehen | 2 → Verschieben | **andere** Größe (Wiederherstellen) |
| App startet das Ziehen selbst aus dem Client (`WM_NCLBUTTONDOWN` mit HTCAPTION) | 1 → unklar | gleiche Größe |

- **Entscheidung:** erst die Trefferzone (`DragHitTest`, `SendMessageTimeout` mit
  `SMTO_BLOCK|SMTO_ABORTIFHUNG`, 100 ms, Punkt per `PhysicalToLogicalPointForPerMonitorDPI` im
  DPI-Raum des Fensters); ist sie unklar, die erste Rechteckänderung. Keine der beiden allein
  reicht: Die Trefferzone versagt bei Alt+Leertaste → Größe und bei Apps, die selbst ziehen; die
  Rechteckänderung irrt beim maximierten Fenster. UIPI blockt die Nachricht an Fenster mit
  Adminrechten → unklar → erste Änderung.
- **Erste Änderung im Detail:** gleiche Größe = Verschieben; neue Größe nur dann Größe ändern, wenn
  eine Kante liegen blieb (die gegenüber dem gezogenen Rand). Ein maximiertes (`IsZoomed`) oder
  angedocktes Fenster bekommt beim Verschieben seine normale Größe (`GetWindowPlacement`) an neuer Stelle
  → Verschieben. Grenze: Das Ereignis kommt etwas verspätet; unter Last kann der Zeiger
  die Randzone schon verlassen haben (dann unklar → erste Änderung, beim Umriss-Ziehen evtl.
  falsche Zone).
- **Erst bei der ersten Änderung handeln:** Ein einfacher Klick auf eine Titelleiste betritt
  dieselbe Schleife (MOVESIZESTART/END ohne Bewegung). Würde schon der Start den Zoom heben, gliche
  jeder Klick auf eine Titelleiste einem Zoom-Flackern. Die Quelle meldet den Start deshalb erst
  bei der ersten Rechteckänderung (`EVENT_OBJECT_LOCATIONCHANGE`, nur für Prozess und Thread des
  gezogenen Fensters, nur während des Ziehens).
- **Umriss-Ziehen** („Fensterinhalt beim Ziehen anzeigen“ aus, `SPI_GETDRAGFULLWINDOWS`): Das
  Fenster bewegt sich erst beim Loslassen, also kommen keine Änderungen. Dann zählt der Zeiger,
  sobald er die Ziehschwelle (`SM_CXDRAG`/`SM_CYDRAG`) überschreitet
  (Timer 50 ms). Mit gedrückter Maustaste zählt sie ab dem Start, sonst ab der ersten Abfrage,
  weil Alt+Leertaste → Verschieben den Zeiger selbst umsetzt; die
  Verschiebung beim Loslassen startet nichts. Die Art kommt dann nur aus der Trefferzone (sonst
  „unbekannt“, zählt nur, wenn beide Schalter an sind). Code: `DragStartProbe`.
- **Monitorwechsel:** aus dem Zeiger (`MonitorFromPoint` bei jeder Fensteränderung), nicht aus
  dem Fensterrechteck. Gemeldet wird nur ein Wechsel.
- **Sicherung:** Ein Fenster, das beim Ziehen abstürzt, sendet kein END. Ein Timer (250 ms, nur
  während des Ziehens) prüft `GUI_INMOVESIZE` und beendet sonst das Ziehen („lost“).
- **Nur wenn es etwas bewirkt:** Bei Zoom aus, Modus Angedockt, Aktion „unverändert“ oder beiden
  Schaltern aus wird kein Fenster geprüft (keine Trefferzone, kein Standort-Hook, kein Timer, kein
  Ablauf); nur der globale MOVESIZE-Hook bleibt.
- **Monitoranordnung:** Ein Wechsel fällt nur auf, wenn der Zeiger eine beim Start unbekannte
  Monitor-ID erreicht (Gerätenamen bleiben oft gleich). Reicht, weil Stufen nur während des Ziehens
  leben und das Ende alle freigibt.
- Eigener Thread `ZoomIN Window Drag` (`Platform/WindowDrag`), nicht die Hooks des Filters pro
  Fenster (`WindowEventHooks` hat eine statische Instanz für seinen Thread und listet nach jedem
  Ereignis alle Fenster).

**App-Matrix:** Automatisch nur mit eigenen Fenstern geprüft (Agents dürfen keine Nutzer-Apps
bedienen). Erwartung aus der Technik, Sichtprüfung durch einen Menschen offen:
| App | Erwartung | Geprüft |
|---|---|---|
| Win32 mit Systemtitelleiste (Testfenster) | Start/Ende, Verschieben/Größe sicher | ✓ Spike |
| Explorer | Systemschleife → wie oben | offen |
| Edge/Chrome: Fenster ziehen | eigene Titelleiste antwortet auf `WM_NCHITTEST` mit Titel → Verschieben | offen |
| Edge/Chrome: Tab herausziehen | Chromium zieht das neue Fenster mit der Systemschleife (`SC_MOVE`) → Start bei der ersten Bewegung; sonst bekannte Einschränkung | offen |
| Teams (neu, WebView2), VS Code (Electron) | eigene Titelleiste per `WM_NCHITTEST` → wie Chrome | offen |
| Office | Systemschleife | offen |
| Windows-Terminal | eigene Titelleiste per `WM_NCHITTEST` | offen |
| Einstellungen-App (UWP, ApplicationFrameHost) | Systemschleife am Rahmenfenster | offen |
| Fenster mit Adminrechten | Ereignisse kommen, Trefferzone blockiert → erste Änderung entscheidet | offen |

Apps, die ihr Fenster selbst per `SetWindowPos` verschieben (ohne Systemschleife), melden kein
MOVESIZESTART und bleiben bekannte Einschränkung (Plan B nur nach Rücksprache).
