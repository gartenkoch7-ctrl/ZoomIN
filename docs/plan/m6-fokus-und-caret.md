# M6 – Fokus- & Caret-Tracking

**Ziel:** Der Zoom folgt dem Tastaturfokus (Tab, Menüs, Dialoge) und dem Textcursor beim
Schreiben – Kernfunktion für Tastaturnutzer.
**Demo:** Im Editor tippen → Zoom folgt dem Textcursor; Tab durch einen Dialog → Zoom
springt weich zum fokussierten Element, das farbig umrahmt ist.
**Voraussetzungen:** M2 (kann parallel zu M3–M5 laufen; Hervorhebung braucht M5).

## Aufgaben

### 1. Tracking-Logik (`Core/Tracking/`)
- [ ] `TrackingEvent` (struct): Quelle, Rechteck, Zeitstempel
- [ ] `TrackingArbiter`: wer bestimmt den Viewport?
      Regel-Vorschlag: zuletzt aktive Quelle gewinnt; Mausbewegung > Schwellwert übernimmt;
      Caret vor Fokus, solange getippt wird
- [ ] Weiches Schwenken (Pan-Easing) statt Springen; kein Schwenken, wenn Ziel schon sichtbar
- [ ] Mauszeiger wird **nicht** verschoben (siehe ADR-0003)
- [ ] Unit-Tests für alle Regeln und Übergänge

### 2. Fokus (`Platform/Automation/`)
- [ ] UIA-Client auf eigenem MTA-Thread, `FocusChangedEvent`
- [ ] `BoundingRectangle` holen (physische Pixel, DPI beachten)
- [ ] Zeitlimit für UIA-Aufrufe (hängende Apps dürfen ZoomIN nicht blockieren)
- [ ] Fallback `GetGUIThreadInfo` / `WinEvent EVENT_OBJECT_FOCUS`

### 3. Caret (`Platform/Automation/`)
- [ ] Primär: UIA `TextPattern2.GetCaretRange` → Rechteck
- [ ] Fallback: `GetGUIThreadInfo().rcCaret`, `EVENT_OBJECT_LOCATIONCHANGE` für Caret
- [ ] Throttling: max. 1 Update pro Frame weiterreichen

### 4. Hervorhebung
- [ ] Fokusrahmen und Caret-Marker über den Shader aus M5 (Farbe, Dicke konfigurierbar)

### 5. Kompatibilitätsmatrix
Testen und Ergebnis in `docs/kb/eingabe-und-tracking.md` eintragen:

| App | Fokus | Caret |
|---|---|---|
| Notepad, Explorer, Einstellungen | | |
| Edge, Chrome, Firefox | | |
| Word, Outlook, Excel | | |
| VS Code, Teams (Electron) | | |
| Konsole / Windows Terminal | | |

## Akzeptanzkriterien
- [ ] Fokus- und Caret-Tracking funktioniert in mind. 80 % der Matrix
- [ ] Eine hängende App verursacht keinen Ruckler im Zoom
- [ ] Tracking-Quellen einzeln abschaltbar (Settings-JSON)
- [ ] Core-Tests ≥ 90 % für `Tracking/`

## Offene Fragen / Risiken
- Electron/Chromium liefern UIA oft erst, wenn Barrierefreiheit erkannt wird → prüfen.
- Excel-Zellen und Konsolen-Caret sind bekannte Problemfälle.
