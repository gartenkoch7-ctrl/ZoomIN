# ADR-0024: Intelligentes Invertieren im Shader

- **Status:** Angenommen (Umsetzung nach Spike)
- **Datum:** 2026-11-06
- **Ergänzt:** [ADR-0020](0020-filter-pro-fenster.md) und [ADR-0023](0023-filter-pro-fenster-titelleiste.md)
  (bleiben gültig)

## Kontext
Der Filter pro Fenster entscheidet hell oder dunkel für das **ganze** Fenster. Viele Fenster sind
gemischt. Ein Beispiel ist die Systemsteuerung im dunklen Windows-Design: eine helle Inhaltsfläche
unter einer dunklen Navigations- und Adressleiste. Beim Invertieren wird diese Leiste hell und
blendet. Die Titelleisten-Logik (ADR-0023) löst das nur für die Titelleiste. Auch der Monitorfilter
(`Strg+Alt+C`) dreht dunkle Flächen heute ins Helle. Der Shader wendet bisher nur eine affine
Farbmatrix je Bereich an und kennt keine Helligkeit der Umgebung.

## Entscheidung (Grilling 2026-11-06)
1. **Neuer Filter** „Invertiert (intelligent)“ (`ColorFilter.InvertedSmart`), wählbar überall, wo
   Filter wählbar sind: Monitor, Automatik-Filter, Regeln je App. Er wirkt im Vollbild-Zoom, in der
   Lupe und im Angedockt-Modus. Das klassische „Invertiert“ bleibt.
2. **Entscheidung je Bildstelle, live:** Ein verkleinertes Umgebungsbild des Desktops (je Monitor,
   nur neu bei geändertem Desktop) liefert die mittlere Helligkeit der Umgebung und ein Maß für
   Farbigkeit und Detailreichtum. Der Shader liest es bilinear in **Quell-Koordinaten** und
   invertiert mit einer weichen Schwelle nur klar helle Umgebungen. Dunkle Bereiche bleiben, und am
   Übergang wird gemischt.
3. **Fotos und Videos** gelten als bunte, detailreiche Umgebung und bleiben original. Ein Schalter
   unter „Erweitert“ ist ab Werk an. Wo invertiert wird, bleibt der **Farbton erhalten** (Matrix wie
   `InvertedKeepHue`).
4. **Kein Flackern:** Ein breiter weicher Bereich statt einer harten Schwelle, dazu eine zeitliche
   Glättung (~150 ms, unabhängig von der Bildrate) über eine kleine Gedächtnis-Textur je Monitor.
5. **Fenster-Einstufung und Titelleiste bleiben.** Sie entscheiden weiter, welche Fenster gefiltert
   werden und wann das Overlay pausiert. Innerhalb wirkt „intelligent“.
6. **Budget und Rückfall:** höchstens 1 ms zusätzliche GPU-Zeit pro Frame bei 1440p auf
   Onboard-Grafik. Fehlt eine GPU-Funktion (Feature-Level 10_0 muss reichen) oder wird das Budget
   gerissen, gilt „Invertiert, Farbton erhalten“. Das wird geloggt und in der Diagnose angezeigt.
7. **Ab Werk:** Der Automatik-Filter wird „intelligent“. Bestehende Einstellungen werden nur
   umgestellt, wenn der Automatik-Filter noch auf dem alten Werkswert „Invertiert“ steht.
   Monitorfilter und App-Regeln bleiben, wie der Nutzer sie gewählt hat.

Schwelle, Umgebungsgröße, Foto-Kriterium und Glättung legt der Spike fest. Sie werden hier
nachgetragen und sind keine Einstellungen.

## Nachtrag Spike (2026-11-06)
- Kachel 16 px, Umfeld 32 px, Probe hell ab Luma 0,55–0,75, Glättung τ = 50 ms.
- Statt einer weichen Schwelle entscheidet jede Kachel **binär** nach Mehrheit und speichert die
  Luma ihres Hintergrunds. Magnify skaliert **kantenerhaltend** hoch: bilinear × Ähnlichkeit von
  Pixel- und Hintergrund-Luma, σ = 0,12. Die weiche Schwelle gab an jeder Kante einen grauen Saum
  (Fehler beim Hovern in der Systemsteuerung). Weich bleibt nur die zeitliche Glättung.
- Foto: Chroma 0,10–0,25 × Farbstreuung 0,06–0,15.
- GPU auf der RTX 3060 Ti bei 1440p: +0,064 ms pro Frame.

## Konsequenzen
- ✅ Gemischte Fenster und der ganze Monitor sehen sauber aus; das gilt auch, wenn sich der Inhalt
  ändert (Navigieren, Scrollen).
- ✅ Icons, Links und Fotos behalten ihre Farben.
- ⚠️ Neuer GPU-Durchgang und neue Texturen je Monitor. Sie werden beim Start oder bei Resize
  angelegt (keine Allokation im Render-Loop) und laufen nur, wenn „intelligent“ irgendwo aktiv ist.
- ⚠️ Saum an Kanten zwischen hell und dunkel, bei hohem Zoom breiter; Text auf mittlerem Grau kann
  Kontrast verlieren.
- ⚠️ Die Palette wächst um einen Eintrag, und ein Eintrag ist nicht mehr nur eine Matrix: Shader und
  `WindowConstants` ziehen mit.
- ⚠️ Kein Gerät mit Onboard-Grafik zum Messen; das Budget wird zunächst hochgerechnet.
- ⚠️ Sichtprüfung durch einen Menschen nötig (Overlay nicht in Screenshots).
