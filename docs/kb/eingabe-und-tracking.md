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
| Caret | `GetGUIThreadInfo` (System-Caret) ~30×/s | Apps ohne System-Caret liefern nichts |

`Core/Tracking/AnchorController` entscheidet pro Monitor, welche Quelle gewinnt (siehe M6-Plan).

## Kompatibilität Fokus / Caret (M6)
| Anwendung | Fokus | Caret | Quelle |
|---|---|---|---|
| WinForms/Win32-Textfelder | ✅ | ✅ | automatische Probe 2026-10-30 |
| Notepad, Explorer, Einstellungen | ? | ? | offen – Sichttest |
| Edge, Chrome, Firefox | ? | ? | offen |
| Word, Outlook, Excel | ? | ? | offen |
| VS Code, Teams, Discord | ? | ? | offen |
| Windows Terminal | ? | ? | offen |

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
