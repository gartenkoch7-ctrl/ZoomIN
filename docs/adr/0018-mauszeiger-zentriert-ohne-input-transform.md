# ADR-0018: Mauszeiger zentriert – ohne Input-Transform

- **Status:** Angenommen (ersetzt den Punkt „Maus zentriert erst mit Input-Transform“ aus ADR-0003)
- **Datum:** 2026-11-03

## Kontext
ADR-0003 vergrößert um den Mauszeiger als Fixpunkt. Der Zeiger erscheint dort, wo er auf dem
Desktop liegt, und rutscht deshalb am Monitorrand an den Rand der Vergrößerung. Nutzerwunsch:
„Die Maus soll so gut wie immer in der Mitte stehen.“ ADR-0003 nahm an, dass dafür
`MagSetInputTransform` (UIAccess, Signatur) nötig ist.

## Erkenntnis
Das stimmt nicht. ZoomIN blendet den Systemzeiger beim Zoomen aus (ADR-0011) und zeichnet ihn
selbst an der Stelle, an der sein Desktop-Punkt **angezeigt** wird (`FrameMapping.Map`). Ein Klick
landet am echten Mauspunkt, und genau dieser Punkt ist unter dem gezeichneten Zeiger zu sehen.
Bild und Klick stimmen also bei **jedem** Fixpunkt überein, nicht nur beim Fixpunkt = Maus.
Ein Input-Transform wäre nur nötig, um die Mausgeschwindigkeit an den Zoom anzupassen.

## Entscheidung
- Neue Einstellung „Mauszeiger in der Mitte halten“ (`TrackingOptions.CenterMouse`),
  **standardmäßig an**. `AnchorController` rechnet die Mausposition mit
  `ViewCentering.AnchorFor` in den Fixpunkt um, der sie in die Bildmitte legt (Vollbild, Lupe).
- Am Monitorrand bleibt die Ansicht stehen; der Zeiger rückt dann Richtung Rand.
- Angedockt bleibt unverändert (der Streifen zentriert schon selbst).
- Aus = bisheriges Verhalten (ADR-0003).

## Konsequenzen
- ✅ Zeiger steht fest in der Bildmitte, das Bild läuft mit; kein UIAccess nötig.
- ⚠️ Bei hohem Zoom wandert der Inhalt pro Mauspixel um `z` Bildschirmpixel – wirkt schneller
  als der Fixpunkt. Wer das nicht mag, schaltet die Einstellung aus.
- ⚠️ Schlägt `MagShowSystemCursor` fehl, sieht man den echten Zeiger an anderer Stelle als den
  gezeichneten (Log-Warnung existiert).
