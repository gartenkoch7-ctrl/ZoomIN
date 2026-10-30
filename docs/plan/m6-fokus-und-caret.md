# M6 – Fokus- & Caret-Tracking

**Ziel:** Der Zoom folgt dem Tastaturfokus (Tab, Menüs, Dialoge) und dem Textcursor beim
Schreiben – Kernfunktion für Tastaturnutzer.
**Demo:** Im Editor tippen → Zoom folgt dem Textcursor; Tab durch einen Dialog → Zoom
schwenkt zum fokussierten Element, das farbig umrahmt ist.
**Voraussetzungen:** M2 (parallel zu M3–M5 möglich; Hervorhebung braucht M5).

## Entscheidungen (Grilling 2026-10-30)
- Fokus-Schwenk **einstellbar: weich (~200 ms) oder sofort**; Standard **weich**.
  Element schon sichtbar → keine Bewegung
- Textcursor mit **Randabstand** (Standard 20 %): Ausschnitt bewegt sich erst am Rand
- Maus bewegt (über Schwellwert) → **gleitend zurück zur Maus**; der Mauszeiger wird nie
  automatisch versetzt (ADR-0003)
- **Fokusrahmen standardmäßig an**, Farbe/Dicke einstellbar

## Aufgaben

### 1. Tracking-Logik (`Core/Tracking/`)
- [ ] `TrackingEvent` (struct): Quelle, Rechteck, Zeitstempel
- [ ] `TrackingArbiter`: zuletzt aktive Quelle gewinnt; Caret vor Fokus während getippt wird;
      Mausbewegung über Schwellwert übernimmt
- [ ] `PanController`: weich/sofort, „nur bewegen, wenn nötig“, Randabstand für Caret
- [ ] Unit-Tests für alle Regeln und Übergänge

### 2. Fokus (`Platform/Automation/`)
- [ ] UIA-Client auf eigenem MTA-Thread, `FocusChangedEvent`, `BoundingRectangle`
- [ ] Zeitlimit für UIA-Aufrufe (hängende Apps blockieren nichts)
- [ ] Fallback `WinEvent EVENT_OBJECT_FOCUS`

### 3. Caret (`Platform/Automation/`)
- [ ] UIA `TextPattern2.GetCaretRange`; Fallback `GetGUIThreadInfo().rcCaret`
- [ ] Max. 1 Update pro Frame weiterreichen

### 4. Hervorhebung
- [ ] Fokusrahmen und Caret-Marker über den Shader aus M5

### 5. Kompatibilitätsmatrix
Testen und Ergebnis in `docs/kb/eingabe-und-tracking.md` eintragen:

| App | Fokus | Caret |
|---|---|---|
| Notepad, Explorer, Einstellungen | | |
| Edge, Chrome, Firefox | | |
| Word, Outlook, Excel | | |
| VS Code, Teams, Discord (Electron/WebView) | | |
| Windows Terminal | | |

## Akzeptanzkriterien
- [ ] Fokus- und Caret-Tracking in ≥ 80 % der Matrix
- [ ] Hängende App verursacht keinen Ruckler
- [ ] Weich/sofort, Randabstand und Fokusrahmen einstellbar (Settings-JSON)
- [ ] Core-Tests ≥ 90 % für `Tracking/`

## Offene Fragen / Risiken
- Chromium/Electron liefern UIA oft erst nach Erkennung einer Hilfstechnologie → prüfen.
- Excel-Zellen und Konsolen-Caret sind bekannte Problemfälle.
