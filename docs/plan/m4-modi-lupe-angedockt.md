# M4 – Zoom-Modi: Lupe & Angedockt

**Ziel:** Neben Vollbild zwei weitere Modi über eine gemeinsame Abstraktion.
**Demo:** `Strg+Alt+F` / `L` / `D` springen direkt in Vollbild / Lupe / Angedockt.
**Voraussetzungen:** M3.

## Entscheidungen (Grilling 2026-10-30)
- Lupe: **Rechteck**, Größe anpassbar (`Strg+Alt+Umschalt+Pfeiltasten`); rund erst nach MVP
- Angedockt: **Platz reservieren** per AppBar – Fenster passen sich in den Restbereich ein
- **Ein Hotkey je Modus:** `Strg+Alt+F` Vollbild, `Strg+Alt+L` Lupe, `Strg+Alt+D` Angedockt
  → [ADR-0008](../adr/0008-tastenkuerzel-strg-alt.md)

## Aufgaben

### 1. Abstraktion (`Core/Modes/`)
- [ ] `IZoomMode`: pro Frame Ziel-Rechteck (wo gezeichnet) und Quell-Rechteck (was vergrößert)
- [ ] `FullscreenMode` (bestehende Logik umziehen), `ModeSwitcher` mit Übergangsanimation
- [ ] Modus im `RenderSnapshot`, aktiver Modus wird gespeichert

### 2. Lupe (`Core/Modes/Lens/`, `Rendering/Overlay/`)
- [ ] Rechteck um den Mauszeiger, Standard 600×300 (physische px, DPI-skaliert)
- [ ] Fixpunkt Maus → Klicks treffen; am Monitorrand Lupe im Monitor halten (Tests!)
- [ ] Transparent außerhalb (DComp-Alpha aus ADR-0006), Rahmen im Shader
- [ ] Größe per Hotkey in Schritten, Min/Max

### 3. Angedockt (`Core/Modes/Docked/`, `Platform/Shell/`)
- [ ] Position oben/unten/links/rechts, Größe in Prozent (Standard: oben, 40 %)
- [ ] Platz per `SHAppBarMessage` reservieren; bei Beenden freigeben
- [ ] Watchdog gibt AppBar nach Absturz frei
- [ ] Zeigt Vergrößerung um die Maus im Restbereich; Klicks im angedockten Bereich sind
      reine Anzeige (dokumentieren)
- [ ] Angedockter Bereich wird nicht selbst mit vergrößert

### 4. Tests & Doku
- [ ] Unit-Tests aller Rechteck-Berechnungen inkl. Ränder und Multi-Monitor
- [ ] WARP-Test: Lupe mit Rahmen und Transparenz
- [ ] KB: Modi-Abschnitt in `eingabe-und-tracking.md`

## Akzeptanzkriterien
- [ ] Alle drei Modi auf jedem Monitor, Wechsel per Direkt-Hotkey ohne Flackern
- [ ] Klicks treffen in Vollbild und Lupe
- [ ] Angedockt: maximierte Fenster passen in den Restbereich
- [ ] Alle Modi in Teams/Zoom/Discord/Browser-Freigabe unsichtbar
- [ ] Nach Beenden **und** nach Absturz ist der AppBar-Platz wieder frei

## Offene Fragen / Risiken
- Taskleiste und AppBar auf derselben Seite → Reihenfolge testen.
