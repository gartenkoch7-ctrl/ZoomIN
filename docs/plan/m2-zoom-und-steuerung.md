# M2 – Flüssiger Zoom & Steuerung

**Ziel:** Logik aus dem Durchstich sauber und getestet in Core; stufenloser, animierter Zoom
mit Hotkeys und Mausrad.
**Demo:** `Strg+Mausrad` zoomt weich von 1× bis 16×; `Strg+Alt+Z` gleitet ein und aus.
**Voraussetzungen:** M1.

## Entscheidungen (Grilling 2026-10-30)
- Zoombereich **1×–16×**, stufenlos; ein Schritt = Faktor **×1,25** (multiplikativ)
- **Strg+Mausrad** zoomt (anpassbar); ZoomIN **verschluckt** das Ereignis und hat Vorrang vor dem
  Zoom der Anwendungen → [ADR-0008](../adr/0008-tastenkuerzel-strg-alt.md)
- Tastatur: `Strg+Alt+Bild↑ / Bild↓` (mit Tastenwiederholung)
- Einschalten **und Ausschalten** gleiten (Standard **150 ms**, einstellbar, 0 = sofort)
- Zoom aus: Pipeline bleibt **warm**, Render-Thread schläft (kein Capture, kein Rendering)
- Strg+Rad hoch bei „aus“ startet bei 1× und wächst; bis 1× zurückdrehen schaltet aus

## Aufgaben

### 1. Geometrie (`Core/Viewport/`) – bereits in M1
- [x] `PhysicalPoint`, `PhysicalRect`, `ViewportCalculator` (Fixpunkt) mit Tests

### 2. Zoom (`Core/Zoom/`)
- [x] `ZoomLimits` (1, 16, ×1,25, multiplikative Schritte)
- [x] `ZoomAnimator`: Easing in Log-Skala, bildratenunabhängig, kein Überschwingen
- [x] `ZoomModel`: Ziel, letzte Stufe, Toggle, Step; thread-sicher, Render-Thread liest lock-frei
- [x] Tests inkl. 60 vs. 144 Hz gleich schnell

### 3. Render-Thread (`Rendering/RenderLoop/`)
- [x] Pipeline warm halten, bei 1× Overlay ausblenden und auf Ereignis schlafen
- [x] Animation pro Frame mit echter Frame-Zeit
- [x] Pipeline-Fehler → neu aufbauen (alle 2 s, nur solange Zoom an)
- [x] Allokationsfreie Capture-Aufrufe (`Interop/DxgiInterop.cs`)

### 4. Eingabe (`Platform/Input/`)
- [x] `Win32HotkeyService` mit optionaler Tastenwiederholung
- [x] `Win32WheelHook` (`WH_MOUSE_LL`, eigener Thread, Ereignis wird verschluckt)

### 5. Einstellungen (`Core/Settings/`, `Platform/Settings/`)
- [x] `AppSettings` (Schema 1): letzte Stufe, Animationsdauer, Hotkeys, Mausrad-Modifier
- [x] `JsonSettingsStore`: `%AppData%\ZoomIN\settings.json`, atomar, defekte Datei → `.broken` + Standard
- [x] Speichern entprellt (1 s) und beim Beenden

### 6. Benchmarks & Allokation
- [x] `tests/ZoomIN.Benchmarks/HotPathBenchmarks.cs`
- [x] Allokationstests (0 B) für Viewport, Animator, Modell, Shader-Draw

## Akzeptanzkriterien
- [x] Core-Tests grün (53 Tests gesamt), Allokation 0 B
- [x] Einschalten sichtbar nach 2–16 ms (Ziel: nächster Frame) – automatisch gemessen
- [x] Strg+Mausrad schaltet ein/aus, Stufe wird gespeichert – automatisch geprüft
- [ ] 1× → 16× ohne Ruckeln bei 60 **und** 144 Hz (Sichttest)
- [ ] In Edge/Word zoomt bei `Strg+Mausrad` nur ZoomIN, nicht die Anwendung (Sichttest)
- [ ] Belegter Hotkey → Hinweis im Fenster statt Absturz (manuell mit belegtem Kürzel)
- [ ] Core-Abdeckung ≥ 90 % messen (`dotnet test --coverage`)

## Offene Fragen / Risiken
- Ausblenden dauert bis zum vollständigen Verschwinden ≈ 390 ms (Einrasten bei exakt 1×) →
  bei Bedarf Schwelle anheben ([performance.md](../kb/performance.md)).
- Windows entfernt zu langsame LL-Hooks still → Hook-Thread überwachen (M3 Robustheit).
- Spiele/Anti-Cheat reagieren evtl. auf Hooks → beobachten.
