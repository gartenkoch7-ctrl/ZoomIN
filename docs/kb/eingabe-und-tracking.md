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

## Mauszeiger in der Mitte (Standard, ADR-0018)
`TrackingOptions.CenterMouse` (Seite „Verfolgung“, standardmäßig an): `AnchorController` legt den
Fixpunkt mit `ViewCentering.AnchorFor` so, dass die Maus in der **Bildmitte** angezeigt wird
(Vollbild, Lupe). Am Monitorrand bleibt die Ansicht stehen und der Zeiger rückt zum Rand.
Angedockt ist unverändert. Klicks treffen trotzdem: Der Systemzeiger ist beim Zoomen
ausgeblendet (ADR-0011), ZoomIN zeichnet ihn dort, wo sein Desktop-Punkt angezeigt wird, und der
Klick landet genau an diesem Desktop-Punkt. Ein Input-Transform ist dafür **nicht** nötig.
- Folge: Pro Mauspixel wandert der Inhalt um `z` Bildschirmpixel (der Zeiger steht still).
- Tests: `MouseCenteringTests` (Mitte, Klickpunkt unter dem Zeiger über den ganzen Monitor,
  Monitorrand, Zoomwechsel, Rückkehr vom Fokus). Benchmark `AnchorCenterMouse` 19 ns, 0 B.
- Aus = Fixpunkt an der Maus (ADR-0003).

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

### Caret-Formen (E2, M18)
- Formen: **Balken** (Werk, wie bis M17), **Keile** über und unter dem Caret, **Rahmen** um das
  Zeichen, **Kreis** (Ring). Farbe wie auf der Zeigerseite (sechs Vorgaben + Farbwähler), Deckkraft
  fest 85 %. Dicke dünn/mittel/dick, gehört zum Profil.
- Geometrie rein in `Core/Tracking/CaretMarkerGeometry` (je Frame, ohne Allokation): Linienstärke
  = Grundwert × Zoom (Zoom gedeckelt bei 8× wie beim Zeiger), Mindestwerte 2/4/8 px. „Mittel“ =
  `max(4, Zoom)` – zusammen mit `max(Linie, Caretbreite)` sieht der Balken aus wie vor E2.
- Keile: Höhe `2 × Linie + 0,2 × Caret-Höhe` (so wirkt die Dicke bei jedem Zoom), Breite doppelt so
  groß, Spitze berührt den Caret. Shader `PSWedge` (Dreieck per Abstandsfunktion, gespiegelt wie der
  Zeiger über `InnerOffset/InnerScale`). Kein Rückfall auf den Balken: Shader sind beim Build
  kompiliert und durch `ShaderBytecodeTests` geprüft – ein Rückfall wäre toter Code und würde einen
  echten Geräteverlust beim Start verdecken.
- Rahmen: Die Caret-Kette liefert meist nur eine Linie. Ist der Caret mindestens 0,3 × so breit wie
  hoch, gilt er als Zeichenzelle; sonst wird ein Zeichen von 0,5 × Höhe rechts vom Caret
  angenommen.
- Kreis: bestehender `PSRing`, Radius halbe Caret-Höhe + 2 Linien.
- Am Rand der sichtbaren Fläche (Monitor, Lupe, Streifen) wird jedes Teil **verschoben, nicht
  beschnitten** – Keile über der ersten Zeile bleiben ganz sichtbar (liegen dann aber über der
  Zeile; Sichtprüfung offen). Gezeichnet wird nur, solange die Caret-Mitte in der Fläche liegt
  (`InView`): Knapp jenseits des Lupenrands würde die Markierung sonst auf andere Buchstaben
  geschoben oder über den unvergrößerten Desktop neben der Lupe gemalt.
- „Mittel“ entspricht dem alten Balken für Carets ab 1 px Breite; ein Caret mit Breite 0 (manche
  UIA-Anbieter) wird jetzt mit dem Zoom breiter (8× → 8 px statt 4 px).
- Geschätzter Caret (`CaretEstimated`) wird weiter nicht markiert (`CaretMarkerGeometry.TryGetCaret`).

### Anzeige-Bedingungen und Haltetaste (E1, M18)
- Fadenkreuz, Caret-Markierung und Fokusrahmen haben je eine Bedingung (Profil): **immer** (Werk),
  **nur in Ruhe**, **nur in Bewegung**, **nur bei gehaltener Taste**.
- „In Ruhe“ = seit 1 s keine Mausbewegung und kein Tippen (Grenze: genau 1 s gilt als Ruhe).
  „In Bewegung“ ist das Gegenteil – Tippen zählt auch, nicht nur die Maus.
- Haltetaste (für alle Profile, Seite „Tastenkürzel“): Strg, Umschalt oder Alt, links oder rechts,
  Werk **Umschalt rechts**. Der LL-Hook meldet seitenspezifische Codes (`VK_RSHIFT` usw.), nie
  das allgemeine `VK_SHIFT`.
- Gegen Aufblitzen beim Großschreiben: Die Taste zählt erst nach **250 ms**, und wird während des
  Haltens eine andere (Nicht-Modifier-)Taste gedrückt, zählt dieses Halten gar nicht mehr, bis die
  Taste losgelassen wird. Modifier zählen nicht als Tippen: Alt Gr schickt ein linkes Strg mit,
  auch bei Tastenwiederholung, und würde sonst „Alt rechts halten“ abbrechen.
- Eigener Hook `Platform/Input/Win32HoldKeyHook` (WH_KEYBOARD_LL, eigener Thread), getrennt vom
  Aktivitäts-Hook, damit beide unabhängig geändert werden können. Er läuft nur, solange eine
  Markierung auf „Haltetaste“ steht (`App/Markers/MarkerInput`), und lässt jede Taste unverändert
  durch. Mausbewegung und Tippen kommen aus dem bestehenden Aktivitäts-Hook (`PointerActivity`).
- Render-Thread: `MarkerActivity.Sample` (drei `long`: letzte Aktivität, Haltebeginn, letztes Haltesignal; ohne Lock) und `MarkerAlphas` entscheiden je
  Frame. Statt eines veröffentlichten Sichtbarkeits-Snapshots (Plan) rechnet der Render-Thread
  selbst – sonst bräuchte der Wechsel nach 1 s Ruhe einen eigenen Timer-Thread. Der Leerlauf-Pfad
  (M14) läuft ohnehin jede Bildwiederholung, eine laufende Überblendung (100 ms) zählt als
  Animation und erzwingt das Zeichnen.
- Tastenwiederholung allein reicht nicht (Wiederholung aus bei „Filtertasten“, ein zweiter Modifier
  übernimmt die Wiederholung) und ein Loslassen kann verloren gehen (Win+L, Fenster mit höheren
  Rechten). Darum prüft der Hook während des Haltens alle 400 ms den echten Tastenzustand
  (`GetAsyncKeyState`): gedrückt = Halten bestätigt, los = Halten beendet. Kommt 1,5 s gar kein
  Signal (Hook entfernt), zählt das Halten nicht mehr; der nächste Druck beginnt neu. Ein Wechsel
  der Haltetaste vergisst ein laufendes Halten. Schlägt das Setzen des Hooks fehl, versucht es die
  nächste Änderung einer Bedingung erneut (nicht jeder Schieberegler-Schritt). Entfernt Windows den
  Hook still (zu langsam unter Last), merkt die Zustandsprüfung „Taste unten, Hook sah nichts“
  (Log 2954) und folgt dann dem Tastenzustand – nur die Tipp-Erkennung fehlt dann.
- Offen: „In Ruhe/In Bewegung“ zählen nur Mausbewegung und Tippen ohne Strg/Alt; Mausrad, Klicks
  und Tastenkürzel zählen nicht als Aktivität.
- Offen: Mausklicks und Mausrad beenden ein Halten nicht. Strg als Haltetaste + Strg+Mausrad-Zoom
  oder Umschalt + Klick zeigt die Markierungen nach 250 ms – mit Nutzern prüfen.
- Alt Gr schickt ein linkes Strg mit Scan-Code 0x21D; das zählt nie als Haltetaste „Strg links“.
- Strg als Haltetaste: Das Loslassen nach einem echten Halten (≥ 250 ms) löst nicht zusätzlich die
  Zeiger-Suchhilfe „Strg allein“ aus (`MarkerActivity.ConsumeHoldEnd`, 500 ms, einmalig) – egal,
  welcher der beiden LL-Hooks das Loslassen zuerst sieht.
- Alt als Haltetaste: Weil die Taste nie verschluckt wird, öffnet Alt allein beim Loslassen in vielen
  Programmen das Menü. Steht im Hilfetext; Abhilfe nur durch Verschlucken, was ausgeschlossen ist.
- Eine Markierung, die aus ist (oder Tracking aus, Fadenkreuz bei 1× nicht gezeigt), steht auf
  Deckkraft 0: Ihre Bedingung startet keinen Hook und erzwingt keine Frames. Eingeschaltet blendet
  sie in 100 ms ein, statt kurz voll aufzublitzen.
- Sicherer Bildschirm: Der Sanitizer setzt alle drei Bedingungen auf „immer“ – dort sehen die
  Hooks zu wenig.

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
