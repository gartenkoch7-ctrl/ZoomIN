# ADR-0023: Filter pro Fenster – Titelleiste getrennt einstufen

- **Status:** Angenommen
- **Datum:** 2026-11-06
- **Ergänzt:** [ADR-0021](0021-filter-pro-fenster-umsetzung.md) Nr. 1/2 und [ADR-0022](0022-filter-pro-fenster-nachbesserung.md)
  (bleiben gültig)

## Kontext
Im dunklen Windows-Design zeichnen viele Apps – vor allem Windows-eigene (Einstellungen, Explorer,
Store-Apps) – eine dunkle Titelleiste über einer hellen Seite. Bisher zählte die Titelleiste beim Blick
mit (kann eine helle Seite unter die Schwelle drücken) und wurde mit der Seite invertiert (dunkle
Titelleiste wird hell und blendet). Entscheidung des Nutzers 2026-11-06: „Eigene Messung“.

## Entscheidung
1. **Höhe der Titelleiste** (`TitleBarReader`): Unterkante der DWM-Titelleistenknöpfe
   (`DWMWA_CAPTION_BUTTON_BOUNDS`, findet auch Titelleisten im Client-Bereich), sonst Oberkante des
   Client-Bereichs (`ClientToScreen`). Nur normale Fenster mit `WS_CAPTION`; mehr als ⅓ der
   Fensterhöhe gilt als Fehlmessung (0). Neu gelesen nur bei geänderter Fenstergröße.
2. **Ein Blick, zwei Ergebnisse:** das Fenster wird wie bisher mit **einem** `StretchBlt` kopiert, aber
   in 64×256 statt 64×64 Pixel, damit die Titelleiste (~30 px) mehrere eigene Zeilen behält
   (`BrightnessMeter.IsLightApart`; eine von der Kante geschnittene Zeile zählt zu keinem Teil). Ein
   zweiter `StretchBlt` wäre ein zweiter DWM-Frame: gemessen 33 statt 17 ms je Blick, verworfen;
   64×256 kostet gemessen dasselbe wie 64×64 (16,6 ms).
   Hell/dunkel des Fensters entscheidet **nur die Fläche unter der Titelleiste**; die Titelleiste
   wird mit derselben Schwelle für sich gezählt und mit dem Fenster entschieden (Mehrheit ihrer Blicke).
3. **Anwendung:** Hat ein Fenster einen eigenen Filter und ist seine Titelleiste dunkel, bekommt sie
   einen eigenen Bereich *vor* dem Fensterbereich mit dem Monitorfilter (wie ein dunkles Fenster).
   Helle oder unbekannte Titelleisten werden wie bisher mit dem Fenster gefiltert. Titelleisten-
   Bereiche zählen zum Limit von 64.
4. **Von Hand je App + Fensterart** (`WindowFilterRule.TitleBar`, Detailseite „Filter pro Fenster“,
   Spalte „Titelleiste“): *Automatisch* (Nr. 3) · *Wie das Fenster* · *Nie filtern* (immer Monitorfilter).
   Bei „Keiner“ bleibt auch die Titelleiste original. Das Kürzel ändert nur den Filter, die Wahl für
   die Titelleiste bleibt.

## Konsequenzen
- ✅ Eine dunkle Titelleiste macht eine helle Seite nicht mehr „dunkel“ und blendet nicht nach dem Invertieren.
- ✅ Settings bleiben kompatibel: ohne `TitleBar` im JSON gilt „Automatisch“.
- ⚠️ Fenster mit festem Filter des Nutzers werden nicht gemessen; ihre Titelleiste folgt dem Fenster,
  außer „Nie filtern“ ist gewählt.
- ⚠️ Apps mit eigener Titelleiste ohne DWM-Knöpfe (z. B. manche Electron-Apps ohne `WS_CAPTION`)
  haben keine erkannte Titelleiste – dann hilft nur die Einstellung von Hand für das ganze Fenster.
- ⚠️ Sichtprüfung durch einen Menschen nötig (Overlay nicht in Screenshots).
