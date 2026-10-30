# M4 – Zoom-Modi: Lupe & Angedockt

**Ziel:** Neben Vollbild zwei weitere Modi, sauber über eine gemeinsame Abstraktion.
**Demo:** `Win+Alt+M` wechselt Vollbild → Lupe → Angedockt, jeweils animiert.
**Voraussetzungen:** M3 (Modi werden direkt pro Monitor gebaut).

## Aufgaben

### 1. Abstraktion (`Core/Modes/`)
- [ ] `IZoomMode`: liefert pro Frame Ziel-Rechteck (wo wird gezeichnet) und
      Quell-Rechteck (was wird vergrößert) – reine Mathe, testbar
- [ ] `FullscreenMode` (bestehende Logik umziehen)
- [ ] `ModeSwitcher` mit Übergangsanimation
- [ ] Modus im `RenderSnapshot`

### 2. Lupe (`Core/Modes/Lens/`, `Rendering/Overlay/`)
- [ ] Lupenrechteck um den Mauszeiger, Größe konfigurierbar (z. B. 600×300)
- [ ] Fixpunkt Maus auch in der Lupe → Klicks treffen
- [ ] Am Monitorrand: Lupe bleibt im Monitor, Fixpunkt-Mathe angepasst (Tests!)
- [ ] Overlay transparent außerhalb der Lupe (benötigt Variante aus ADR-0006 mit Alpha)
- [ ] Rahmen (Breite/Farbe) im Shader
- [ ] Größe per Hotkey ändern

### 3. Angedockt (`Core/Modes/Docked/`, `Platform/Shell/`)
- [ ] Bereich oben/unten/links/rechts, Größe in Prozent
- [ ] Platz per AppBar reservieren (`SHAppBarMessage`), damit andere Fenster nicht
      darunter liegen; beim Beenden/Absturz wieder freigeben
- [ ] Angedockter Bereich zeigt Vergrößerung um die Maus im Restbereich
- [ ] Klicks im angedockten Bereich: nur Anzeige (kein Durchklicken) – dokumentieren
- [ ] Eigener angedockter Bereich wird nicht mit vergrößert (Rückkopplung vermeiden)

### 4. Tests & Doku
- [ ] Unit-Tests aller Rechteck-Berechnungen inkl. Ränder und Multi-Monitor
- [ ] KB: Modi-Abschnitt in `eingabe-und-tracking.md` ergänzen

## Akzeptanzkriterien
- [ ] Alle drei Modi auf jedem Monitor nutzbar, Wechsel ohne Flackern
- [ ] Klicks treffen in Vollbild und Lupe
- [ ] Angedockt: andere Fenster maximieren sich in den Restbereich
- [ ] Alle Modi in Teams-Aufnahme unsichtbar (inkl. Lupenrahmen)
- [ ] Nach Beenden ist der reservierte AppBar-Platz wieder frei

## Offene Fragen / Risiken
- AppBar-Reservierung nach einem Absturz: Watchdog oder Aufräumen beim nächsten Start.
- Lupe sehr groß bei hohem Zoom → GPU-Last unkritisch, aber testen.
