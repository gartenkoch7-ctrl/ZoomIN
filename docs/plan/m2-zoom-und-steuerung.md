# M2 – Flüssiger Zoom & Steuerung

**Ziel:** Logik aus dem Durchstich sauber und getestet in Core; stufenloser, animierter Zoom
mit Hotkeys und Mausrad.
**Demo:** `Strg+Mausrad` zoomt weich von 1× bis 16×; `Strg+Alt+Z` gleitet ein und aus.
**Voraussetzungen:** M1.

## Entscheidungen (Grilling 2026-10-30)
- Zoombereich **1×–16×**, stufenlos; ein Schritt = Faktor **×1,25** (multiplikativ)
- **Strg+Mausrad** zoomt (anpassbar); ZoomIN **verschluckt** das Ereignis und hat Vorrang vor dem
  Zoom der Anwendungen → [ADR-0008](../adr/0008-tastenkuerzel-strg-alt.md)
- Tastatur: `Strg+Alt+Bild↑ / Bild↓`
- Einschalten: gleitet in ca. **150 ms** auf die **zuletzt benutzte Stufe**; Dauer einstellbar,
  inkl. „sofort“

## Aufgaben

### 1. Geometrie (`Core/Viewport/`)
- [ ] `PhysicalPoint`, `PhysicalSize`, `PhysicalRect` (readonly structs, `double`)
- [ ] `ViewportCalculator.SourceRect(monitorBounds, cursor, zoom)` – Fixpunkt-Formel aus
      [eingabe-und-tracking.md](../kb/eingabe-und-tracking.md); Test beweist: Quellrechteck
      liegt immer im Monitor
- [ ] Tests: Ecken, Mitte, 1×, 16×, krumme Größen, negative Koordinaten

### 2. Zoom-Zustand (`Core/Zoom/`)
- [ ] `ZoomLimits` (1, 16, Schritt ×1,25)
- [ ] `ZoomState`: Ziel/Ist, `Advance(deltaSeconds)`, Easing mit Zeitkonstante aus Settings
      (0 = sofort)
- [ ] An/Aus = Animation zwischen 1× und zuletzt benutzter Stufe (wird gespeichert)
- [ ] Tests: konvergiert, kein Überschwingen, gleich schnell bei 60 und 144 Hz

### 3. Zustandsübergabe (`Core/Rendering/`)
- [ ] `RenderSnapshot` (immutable), `SnapshotPublisher` (atomarer Tausch)
- [ ] Render-Thread nutzt nur Core-Logik

### 4. Eingabe (`Platform/Input/`)
- [ ] `HotkeyService` (`RegisterHotKey`, Message-Only-Fenster, eigener Thread)
- [ ] `WheelZoomHook` (`WH_MOUSE_LL`, eigener Thread): bei konfiguriertem Modifier
      Ereignis verschlucken und Wert weiterreichen – Callback ohne Allokation, ohne Locks
- [ ] Hotkeys und Mausrad-Modifier aus `settings.json` (`Core/Settings/AppSettings`)

### 5. Benchmarks
- [ ] `ViewportCalculator`, `ZoomState.Advance`, Snapshot-Lesen → `0 B`

## Akzeptanzkriterien
- [ ] Core-Testabdeckung ≥ 90 %, Benchmarks `0 B`
- [ ] 1× → 16× ohne Ruckeln, bei 60 und 144 Hz gleich schnell
- [ ] In Edge/Word zoomt bei `Strg+Mausrad` nur ZoomIN, nicht die Anwendung
- [ ] Belegter Hotkey → verständliche Log-Meldung, kein Absturz

## Offene Fragen / Risiken
- Windows entfernt zu langsame LL-Hooks still → Hook-Thread überwachen und neu registrieren.
- Spiele/Anti-Cheat reagieren evtl. auf Hooks → beobachten.
