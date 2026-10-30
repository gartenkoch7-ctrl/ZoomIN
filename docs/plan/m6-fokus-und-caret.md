# M6 – Fokus- & Caret-Tracking

**Ziel:** Der Zoom folgt dem Tastaturfokus (Tab, Menüs, Dialoge) und dem Textcursor beim
Schreiben – Kernfunktion für Tastaturnutzer.
**Demo:** Im Editor tippen → Zoom folgt dem Textcursor; Tab durch einen Dialog → Zoom
schwenkt zum fokussierten Element, das magenta umrahmt ist.
**Voraussetzungen:** M2 (Hervorhebung nutzt die Zeiger-Pipeline aus M5).

## Entscheidungen (Grilling 2026-10-30)
- Fokus-Schwenk **weich (200 ms)** oder sofort (`PanMilliseconds = 0`); nur bewegen, wenn das
  Ziel nicht schon sichtbar ist
- Textcursor mit **Randabstand 20 %**
- Mausbewegung (> 8 px) → **gleitend zurück zur Maus**; Mauszeiger wird nie versetzt (ADR-0003)
- **Alle Modi** folgen (Vollbild schwenkt, Lupe springt, Streifen zeigt die Umgebung)
- **Fokusrahmen Magenta** (an), **Caret-Balken** (an), beide abschaltbar
- **Strg+Alt+T** schaltet Fokus-/Caret-Verfolgung an/aus

## Umsetzung
- **Erkennung (`Platform/Automation/UiaTrackingSource`):** eigener Thread fragt ~30×/s
  UI Automation (`GetFocusedElement` → `CurrentBoundingRectangle`) und `GetGUIThreadInfo`
  (Caret) ab. Polling statt COM-Ereignissen: keine Callback-Objekte, hängende Apps verzögern
  nur diesen Thread.
- **Logik (`Core/Tracking/AnchorController`):** pro Monitor; Caret vor Fokus; Fokuswechsel
  < 0,5 s nach Mausbewegung gelten als von der Maus verursacht und werden ignoriert;
  Fokusrechtecke > 50 % des Monitors werden ignoriert; Zeitschritt pro Frame auf 0,1 s begrenzt.
- **Darstellung:** Zeiger wird immer über die Frame-Abbildung platziert (`FrameMapping`), auch
  wenn der Zoom am Fokus hängt; Systemzeiger bei Zoom ausgeblendet (außer Angedockt).
  Fokusrahmen/Caret-Balken in `HighlightLayer` (`PSRect` in `Cursor.hlsl`).

## Aufgaben
- [x] `TrackingSnapshot`, `TrackingOptions`, `TrackingModel`, `AnchorController` + 10 Tests
- [x] `UiaTrackingSource` + Smoke-Test; Probe im echten Fenster (siehe Matrix)
- [x] `FrameRequest`, `RenderModels`, `FrameMapping`, `HighlightLayer`
- [x] Einstellungen, Strg+Alt+T, Statusfenster

## Kompatibilitätsmatrix
Ergebnisse in [eingabe-und-tracking.md](../kb/eingabe-und-tracking.md#kompatibilität-fokus--caret-m6).

## Akzeptanzkriterien
- [x] Fokus und Caret werden in einem Win32/WinForms-Fenster erkannt (automatisch geprüft)
- [x] Core-Tests für alle Regeln (Sichtbarkeit, Randabstand, Maus-Rückkehr, Maus-Absicht)
- [ ] Sichttest: Tab durch Dialog, Tippen im Editor, Rückkehr zur Maus, Rahmen/Balken
- [ ] Kompatibilitätsmatrix ≥ 80 % (Notepad, Edge, Word, VS Code, Teams, Terminal …)
- [ ] Hängende App verursacht keinen Ruckler (Erkennung läuft getrennt – manuell bestätigen)

## Offene Fragen / Risiken
- Chromium/Electron liefern UIA oft erst, wenn eine Hilfstechnologie erkannt wird.
- Konsole/Terminal und Excel-Zellen haben oft keinen System-Caret.
