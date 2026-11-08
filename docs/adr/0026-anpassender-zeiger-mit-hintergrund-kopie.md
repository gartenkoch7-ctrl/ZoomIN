# ADR-0026: Anpassender Zeiger – Farbpaar nach sichtbarem Hintergrund, auch bei 1×

- **Status:** Vorgeschlagen (Umsetzung in [M15](../plan/m15-anpassender-zeiger.md))
- **Datum:** 2026-11-08

## Kontext
Nutzer wünschen einen Zeiger, der sich wie die Windows-Zeigerart „Invertiert“ dem Hintergrund
anpasst – mit eigenem Farbpaar. ZoomIN zeichnet den Zeiger als vormultipliziertes Bild über dem
vergrößerten Bild (`CursorPass`). Der Shader kennt den Hintergrund nicht; Pixel mit
„Bildschirm invertieren“ (XOR) werden heute schwarz gezeichnet (`CursorPixels`).

Ohne Zoom und Filter ist das Overlay durchsichtig und hat keine Desktop-Kopie; nach M14 ruht die
Duplication im Leerlauf.

## Entscheidung (Grilling 2026-11-08)
1. **Zwei Farben statt Invertierung:** Jeder Zeigerpixel nimmt Farbe A (dunkler Grund) oder B
   (heller Grund), hart an einer einstellbaren Schwelle; der Umriss nimmt die Gegenfarbe. Echte
   Invertierung (1 − Hintergrund, z. B. per Blend-Zustand) scheidet aus: Auf Mittelgrau wird der
   Zeiger grau, und ein Farbpaar ist nicht möglich.
2. **Hintergrund = was man sieht:** Vor dem Zeiger-Durchgang wird ein kleiner Ausschnitt um den
   Zeiger aus dem Back Buffer und aus der Desktop-Kopie kopiert und im Shader zusammengesetzt
   (Back Buffer + Desktop · (1 − α)). So zählen Farbfilter, Fensterfilter und intelligentes
   Invertieren, und es stimmt auch bei durchsichtigem Overlay.
3. **Auch bei 1×:** In diesem Modus läuft die Desktop Duplication auch ohne Zoom und Filter, aber
   **nur auf dem Monitor mit der Maus**; ein neuer Frame entsteht nur bei Zeigerbewegung oder
   Desktop-Änderung im Ausschnitt um den Zeiger.

## Folgen
- + Zeiger ist auf jedem Hintergrund sichtbar, auch auf Grau, auch bei invertierten Filtern.
- + Ein Modus für alle Zoom-Modi; Lupe/Angedockt verhalten sich außerhalb des Zoombereichs wie 1×.
- − Ausnahme von „Leerlauf: Capture gestoppt“ (performance.md) für diesen Modus; Kosten werden
  gemessen und im Messprotokoll festgehalten.
- − Zwei kleine Kopien und ein paar Proben pro Frame im Zeiger-Durchgang (Budget ≤ 0,05 ms Dev-PC).
- − Die Farbe kann bei schnellen Desktop-Änderungen einen Frame nachhängen.
