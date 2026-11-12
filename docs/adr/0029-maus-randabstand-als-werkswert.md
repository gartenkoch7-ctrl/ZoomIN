# ADR-0029: Maus folgt mit Randabstand (Werkswert) statt „Zeiger in der Mitte“

- **Status:** Angenommen (Umsetzung [M17 A1](../plan/features/a1-ruhige-mausverfolgung.md))
- **Datum:** 2026-11-11
- **Bezug:** ergänzt ADR-0018 (Zeiger zentriert ohne Input-Transform) und ADR-0003 (Fixpunkt);
  beide gelten weiter, nur der Werkswert ändert sich.

## Kontext
Bis 1.0 gab es den Schalter „Mauszeiger in der Mitte halten“ (`CenterMouse`, standardmäßig an,
ADR-0018). Dabei wandert das Bild bei jedem Mausschritt um `z` Bildschirmpixel. Das ist bei hoher
Vergrößerung unruhig und kann Schwindel auslösen. Andere Lupen bieten dafür einen Randabstand: Das
Bild bewegt sich erst, wenn der Zeiger an den Rand kommt.

## Entscheidung (Grilling 2026-11-11)
1. Drei Modi `MouseFollowMode`: **Mitte** (ADR-0018), **Randabstand** (neu), **Fixpunkt** (ADR-0003).
   Sie ersetzen den Schalter `CenterMouse`. Der Abstand ist einstellbar (0–50 %, Werk 20 %).
2. **Randabstand ist der Werkswert.** Am Rand schiebt sich das Bild exakt mit (wie beim Caret),
   ohne Verzögerung und ohne Springen. Klicks treffen wie bei ADR-0018: Der Zeiger wird an seinem
   angezeigten Desktop-Punkt gezeichnet, ein Input-Transform ist nicht nötig.
3. Bestehende Installationen werden beim ersten Start nach dem Update **einmal** auf Randabstand
   umgestellt (`SchemaVersion` 1 → 2), mit einem Hinweis, wo man zurückstellt. Danach gilt die
   Wahl des Nutzers. `CenterMouse` wird nur noch gelesen.
4. Modus und Abstand gehören zum Profil. Angedockt bleibt unverändert, Fokus/Caret auch.

## Folgen
- Weniger Bildbewegung; der Zeiger wandert innerhalb des Bildes.
- Wer bewusst „Mitte“ oder „Fixpunkt“ gewählt hatte, wird einmal umgestellt. Der Hinweis muss
  deutlich sein (Tray-Benachrichtigung, Klick öffnet die Seite).
- Hot Path: ≈ +8 ns je Frame gegenüber „Mitte“, 0 B (siehe `performance.md`).
