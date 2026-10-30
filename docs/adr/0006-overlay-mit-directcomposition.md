# ADR-0006: Overlay-Fenster mit DirectComposition

- **Status:** Angenommen (vorläufig – wird im M1-Durchstich bestätigt oder ersetzt)
- **Datum:** 2026-10-30

## Kontext
Das Overlay muss click-through sein, aus Aufnahmen ausgeschlossen werden
(`WDA_EXCLUDEFROMCAPTURE`) und für Lupe/Angedockt transparente Bereiche haben.
Kandidaten: (A) `WS_EX_NOREDIRECTIONBITMAP` + DirectComposition +
`CreateSwapChainForComposition`, (B) normales Fenster + `CreateSwapChainForHwnd`.

## Entscheidung
Direkt Variante **A** bauen – kein Vergleichs-Spike. Variante B nur, falls A beim
Capture-Ausschluss oder bei der Latenz scheitert.

## Begründung
Lupe und Angedockt (M4) brauchen Alpha-Transparenz; das kann nur A. Ein Vergleich beider
Varianten würde 2–3 Tage kosten, ohne dass B langfristig nutzbar wäre.

## Konsequenzen
- M1 prüft für A: Click-through, Capture-Ausschluss (Teams, Zoom/Webex, Discord, Browser),
  keine Rückkopplung, Latenz bei 60 und 144 Hz.
- Scheitert A → dieses ADR auf „Ersetzt durch …“ setzen und B dokumentieren.
