# ADR-0022: Filter pro Fenster – Nachbesserung nach Test und Review

- **Status:** Angenommen
- **Datum:** 2026-11-06
- **Ersetzt teilweise:** [ADR-0021](0021-filter-pro-fenster-umsetzung.md) Nr. 1 (Thread und
  Kopiermodus der Blicke) und Nr. 2 (Zahl der Bereiche); der Rest von ADR-0021 bleibt gültig.

## Kontext
Messungen am laufenden Dev-Build und ein Code-Review von M12 zeigten:
- Ein GDI-Blick wartet immer auf den nächsten DWM-Frame (≈ 17 ms bei 60 Hz, unabhängig von der
  Größe). Auf dem Tracker-Thread blockierten mehrere Blicke pro Takt die WinEvents bis zu 75 ms –
  ein Menü in einer invertierten App bekam seinen Bereich so mehrere Frames zu spät (weißes Aufblitzen).
- In Lupe und Angedockt mit Zoom war außerhalb der Lupe nichts gefiltert: ein invertiertes Fenster
  wurde beim Einschalten der Lupe wieder hell.
- Mehr als 32 nötige Bereiche fielen still auf den Monitorfilter zurück.

## Entscheidung
1. **Blicke auf eigenem Thread** (`LookWorker`). Der Tracker sammelt die fälligen Blicke mit allem,
   was sie brauchen (`LookRequest`: Fläche, Fenster darüber, Monitore, Schwelle), der Worker kopiert
   und zählt und weckt den Tracker mit einer Nachricht; der Tracker übernimmt die Ergebnisse. Der
   Tracker-Thread wartet nie mehr auf DWM.
2. **Punktabtastung statt HALFTONE** (`COLORONCOLOR`). Gemessen wird der *Flächenanteil* heller
   Pixel; HALFTONE mittelte Text auf Weiß zu Grau und verschob den Anteil. Gleich schnell.
3. **Bis zu 64 Bereiche** (Constant Buffer ≈ 2,5 KB). Werden es mehr, zählt
   `zoomin.windowfilter.regions_dropped` die übrigen, Log 2705 meldet den Beginn.
4. **Außerhalb von Lupe/Streifen wie bei 1×** (Entscheidung des Nutzers 2026-11-06): Ist ein
   Farbfilter an oder braucht ein Fenster einen eigenen Filter, zeichnet das Overlay zuerst das
   ganze Bild in 1× mit Monitor- und Fensterfiltern, darüber die Lupe. Das ändert auch das bisherige
   Verhalten des Monitorfilters (vorher nur in der Lupe). Ohne Filter bleibt außen alles durchsichtig.
5. **Keine Regel ohne eindeutige App:** Programme mit unlesbarem Namen (`unknown`) und Store-Rahmen,
   deren App noch nicht gefunden wurde, bekommen keine Regel (Kürzel, Liste „Hinzufügen“). Ein
   Store-Rahmen wird bei jeder Aktualisierung neu befragt, bis seine App drin ist.
6. **Kürzel auf einem Popup** (Menü, Flyout) wirkt auf das Besitzerfenster.

## Verworfene Alternativen
- **Sofort-Update bei `EVENT_OBJECT_SHOW`, Nach-Update per Timer, höhere Thread-Priorität:**
  ausprobiert, keine messbare Wirkung – die Ursache war der blockierende Blick.
- **Lupe außen nur mit Fensterfiltern:** wäre beim Monitorfilter uneinheitlich gewesen.

## Konsequenzen
- ✅ Popup sichtbar → Bereich da: Median 3,3 ms, 90 % unter 4,5 ms, auch während 16 Blicke laufen
  (vorher Median 3 ms, 90 % bis 52 ms, max. 75 ms).
- ✅ `WindowRegionsCheck` mit 64 Bereichen 65 ns, 0 B.
- ⚠️ Lupe mit Filter: ein zweiter Vollbild-Durchgang pro Frame (GPU), außen wird nicht vergrößert.
- ⚠️ Sichtprüfung der Lupe mit Filter steht aus (Overlay nicht in Screenshots).
