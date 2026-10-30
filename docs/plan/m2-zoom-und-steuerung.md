# M2 – Flüssiger Zoom & Steuerung

**Ziel:** Die Logik aus dem Durchstich sauber und getestet in Core; stufenloser,
animierter Zoom mit Hotkeys und Mausrad.
**Demo:** `Strg+Alt+Mausrad` zoomt weich von 1× bis 16×; An/Aus gleitet ein und aus.
**Voraussetzungen:** M1.

## Aufgaben

### 1. Geometrie (`Core/Viewport/`)
- [ ] `PhysicalPoint`, `PhysicalSize`, `PhysicalRect` (readonly structs, `double`)
- [ ] `ViewportCalculator.SourceRect(monitorBounds, cursor, zoom)`:
      Fixpunkt-Formel aus [eingabe-und-tracking.md](../kb/eingabe-und-tracking.md);
      Quellrechteck liegt mathematisch immer im Monitor → Test beweist das
- [ ] Tests: Ecken, Mitte, Zoom 1×, Zoom 16×, krumme Monitorgrößen, negative Koordinaten
      (Monitor links vom Hauptmonitor)

### 2. Zoom-Zustand (`Core/Zoom/`)
- [ ] `ZoomLimits` (Min 1, Max 16, Schrittfaktor z. B. ×1,25)
- [ ] `ZoomState`: Ziel- und Ist-Wert, `Advance(deltaSeconds)` mit Easing
      (exponentielles Annähern oder kritisch gedämpfte Feder – per Benchmark/Gefühl wählen)
- [ ] Zoom-Stufen multiplikativ (gleich groß wirkende Schritte), nicht additiv
- [ ] An/Aus = Animation zwischen 1× und letztem Wert
- [ ] Tests: konvergiert, überschwingt nicht, unabhängig von Bildrate (60 vs. 144 Hz gleich schnell)

### 3. Zustandsübergabe UI → Render (`Core/Rendering/`)
- [ ] `RenderSnapshot` (immutable): Zoom-Ziel, Modus, Filter-Platzhalter
- [ ] `SnapshotPublisher`: atomarer Austausch (`Volatile.Write`/`Read`)
- [ ] Render-Thread nutzt nur noch Core-Logik, keine eigene Mathe mehr

### 4. Eingabe (`Platform/Input/`)
- [ ] `HotkeyService` über `RegisterHotKey` mit Message-Only-Fenster auf eigenem Thread
- [ ] Mausrad-Zoom mit Modifier über `WH_MOUSE_LL`-Hook auf eigenem Thread;
      Hook-Callback nur Werte weiterreichen (Windows entfernt langsame Hooks!)
- [ ] Hotkeys vorerst aus `settings.json` (`Core/Settings/AppSettings` + einfacher Store)

### 5. Benchmarks
- [ ] `ViewportCalculator`, `ZoomState.Advance`, Snapshot-Lesen → `0 B` Allokation

## Akzeptanzkriterien
- [ ] Core-Testabdeckung ≥ 90 %
- [ ] Benchmarks: `0 B` allokiert
- [ ] Zoom 1× → 16× ohne sichtbares Ruckeln, auf 60 und 144 Hz gleich schnell
- [ ] Hotkey-Kollision (bereits belegt) → verständliche Log-Meldung, kein Absturz

## Offene Fragen / Risiken
- Mausrad-Hook und Spiele/Anti-Cheat: Hook nur aktiv, solange nötig? → beobachten.
- Easing-Parameter sind Gefühlssache → mit 2–3 Testpersonen abstimmen.
