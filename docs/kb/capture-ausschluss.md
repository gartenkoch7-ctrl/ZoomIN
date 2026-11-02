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

## Z-Ebenen: Shell-Menüs liegen über jedem Topmost-Fenster
Windows ordnet Fenster nicht nur nach „topmost“, sondern zusätzlich in **Ebenen (Bands)**. Die
Schnelleinstellungen (Ton, Netzwerk), das Info-Center und das Startmenü liegen in einer Ebene über
allen normalen Topmost-Fenstern. Folge ohne UIAccess (gemeldet 2026-10-30, Windows 11):
- Desktop Duplication nimmt das Menü auf → es erscheint **vergrößert und gefiltert** im Zoom.
- Das echte Menü liegt aber **über** dem Overlay → es bleibt zusätzlich **unvergrößert und
  ungefiltert** an seiner Stelle stehen.

Kein Fensterstil hilft (`WS_EX_TOPMOST`, `SetWindowPos(HWND_TOPMOST)`); `CreateWindowInBand` ist
für normale Prozesse gesperrt. Lösung: der **UIAccess-Build** (ADR-0012) – Topmost-Fenster eines
UIAccess-Prozesses kommen in die UIAccess-Ebene über diesen Menüs. Voraussetzung: signierte EXE in
`Program Files`. ⚠️ Nach dem ersten UIAccess-Install mit Schnelleinstellungen, Info-Center und
Startmenü bestätigen und hier eintragen.

## Pflichttest (manuell, vor jedem Release)
1. Zoom aktivieren (alle drei Modi).
2. Bildschirm freigeben in: **Microsoft Teams**, **Zoom/Webex**, **Discord**,
   **Browser-Freigabe** (Edge/Chrome, z. B. Google Meet); zusätzlich `Win+Shift+S`.
3. Erwartung: Aufnahme zeigt unvergrößerten Desktop, Hervorhebungen ebenfalls unsichtbar.
Ergebnisse mit Windows-Build und App-Versionen hier eintragen.

## Testprotokoll
| Datum | Windows-Build | Tool | Ergebnis |
|---|---|---|---|
| 2026-10-30 | 22631 | GDI `CopyFromScreen` (BitBlt), automatisiert | ✅ unsichtbar – Bild mit 2×-Zoom = Bild ohne Zoom (0,14 % Abweichung, Rauschen 0,08 %); Overlay `affinity=0x11` |
| – | 22631 | Teams / Zoom / Discord / Browser-Freigabe | offen – manueller Test (SuperNova vorher beenden!) |

Hinweis: Beim Testen darf **keine andere Lupe** (SuperNova, Windows-Bildschirmlupe) aktiv sein,
sonst ist nicht unterscheidbar, wessen Zoom zu sehen ist.
