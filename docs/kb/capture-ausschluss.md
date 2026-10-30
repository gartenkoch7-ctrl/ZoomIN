# Unsichtbar bei Bildschirmübertragung

## Anforderung
Teilt der Nutzer seinen Bildschirm (Teams, Zoom, Webex, OBS, Snipping Tool), sehen die
anderen den **normalen, unvergrößerten** Desktop. Nur auf dem physischen Monitor ist der
Zoom sichtbar.

## Lösung
- Wir zeichnen den Zoom in ein eigenes **Overlay-Fenster** (topmost, randlos,
  `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`).
- Darauf: `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` (ab Windows 10 2004).
- Aufnahme-Software, die über Desktop Duplication oder Windows.Graphics.Capture aufnimmt,
  sieht das Fenster nicht → sie sieht den Desktop darunter.
- **Bonus:** Unsere eigene Desktop Duplication sieht das Overlay ebenfalls nicht → keine
  Rückkopplung („Spiegel im Spiegel“).

## Warum nicht die Windows-Magnification-API?
`MagSetFullscreenTransform` verändert die Ausgabe des Desktop Window Managers selbst; das
Ergebnis kann **nicht** aus Aufnahmen ausgeschlossen werden. Deshalb ist sie für die Anzeige
verboten (ADR-0002). `MagSetInputTransform` (nur Eingabe) bleibt für später erlaubt.

## Grenzen
| Fall | Verhalten |
|---|---|
| Teams/Zoom/OBS/Snipping Tool | Zoom unsichtbar ✅ |
| Hardware-Capture (HDMI-Splitter, Capture-Karte, Beamer im Spiegelmodus) | Zoom **sichtbar** – physikalisch nicht vermeidbar |
| Windows < 10 2004 | `WDA_EXCLUDEFROMCAPTURE` fehlt → nicht unterstützt |
| Alte Aufnahme-Software mit GDI-`BitBlt` | ⚠️ unverifiziert – testen und hier dokumentieren |

## Pflichttest (manuell, vor jedem Release)
1. Zoom aktivieren (alle drei Modi).
2. Teams-Testanruf / OBS-Vorschau / `Win+Shift+S`.
3. Erwartung: Aufnahme zeigt unvergrößerten Desktop, Hervorhebungen ebenfalls unsichtbar.
Ergebnisse mit Windows-Build und App-Versionen hier eintragen.

## Testprotokoll
| Datum | Windows-Build | Tool | Ergebnis |
|---|---|---|---|
| – | – | – | – |
