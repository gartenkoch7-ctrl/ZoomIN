# M5 – Farbfilter & Hervorhebung

**Ziel:** Bessere Lesbarkeit durch Farbfilter, Kantenglättung und einen gut sichtbaren
Mauszeiger – alles im selben GPU-Durchgang.
**Demo:** `Strg+Alt+C` schaltet den Filter; Text bleibt bei 8× scharf; großer Zeiger mit
Kontrastrand; `Strg+Alt+S` zeigt die Suchhilfe.
**Voraussetzungen:** M3.

## Entscheidungen (Grilling 2026-10-30)
- Filter: **Invertiert**, **Invertiert mit Farbtonerhalt**, **Graustufen** (normal/invertiert),
  **Gelb auf Schwarz**, **Schwarz auf Gelb**, **Weiß auf Blau**
- Bedienung: `Strg+Alt+C` an/aus, `Strg+Alt+Umschalt+C` nächster Filter (schaltet ihn ein)
- **Filter wirken auch ohne Zoom (1×)** – das Overlay läuft dann bei 1×
- Glättung Standard **text-optimiert** (Catmull-Rom); „Weich“ (bilinear) und „Pixel“ wählbar
- Zeiger: **echte Windows-Form + Kontrastrand**, wächst mit dem Zoom bis **max. 4×**
- Suchhilfe: `Strg+Alt+S`; Automatik nach Stillstand **standardmäßig aus** (Einstellung)
- Systemzeiger ausblenden per `MagShowSystemCursor` → [ADR-0011](../adr/0011-systemzeiger-per-magshowsystemcursor.md)

## Aufgaben

### 1. Core (`Core/Filters/`)
- [x] `ColorMatrix` (3×3 + Offset, Komposition), `ColorFilters` (alle Filter + Kontrast/Helligkeit)
- [x] `FilterSettings`, `Smoothing`, `CursorStyle`, `DisplayEffects` (Matrix vorberechnet)
- [x] `EffectsModel` (Filter an/aus/nächster, Suchhilfe), lock-frei für Render-Threads
- [x] Tests: jeder Filter mit Weiß/Schwarz/Rot, Kontrast, `with` berechnet Matrix neu

### 2. Shader (`Rendering/Shaders/`)
- [x] `Magnify.hlsl`: Farbmatrix, Catmull-Rom (9 Abtastungen), bilinear, Punkt
- [x] `Cursor.hlsl`: Zeiger mit Kontrastrand (gedehntes Alpha), Suchring
- [x] WARP-Tests: Filter, alle Glättungen, harte vs. weiche Kante, 0 B beim Zeichnen

### 3. Zeiger (`Platform/Cursor/`, `Rendering/RenderLoop/CursorLayer`)
- [x] Zeigerform aus `GetCursorInfo`/`GetIconInfo`/`GetDIBits` (farbig, monochrom, mit/ohne Alpha)
- [x] Textur-Cache pro Zeiger-Handle; Zeichnen ohne Allokation
- [x] Position: Vollbild/Lupe am echten Punkt (Fixpunkt), Angedockt umgerechnet in den Streifen
- [x] Systemzeiger nur ausgeblendet, wenn der vergrößerte gezeichnet wird; Watchdog blendet ihn ein

### 4. Suchhilfe
- [x] Ring schrumpft in 1 s auf den Zeiger; funktioniert auch bei Zoom aus
- [x] Automatik nach 3 s Stillstand (Einstellung `AutoLocate`)

## Akzeptanzkriterien
- [x] Filter bei 1× und mit Zoom, an/aus/weiter per Hotkey (automatisch geprüft)
- [x] Absturz → Systemzeiger-Aufräumschritt läuft (automatisch geprüft)
- [ ] Sichttest: Filterfarben, Textschärfe bei 8×, Zeiger + Rand, Suchring
- [ ] Zeiger korrekt für Pfeil, I-Balken, Hand, Größenänderung (Sichttest)
- [ ] Frame-Budget mit Catmull-Rom bei 144 Hz messen
- [ ] Filter und Zeiger bei Bildschirmfreigabe unsichtbar (mit M1-Freigabetest)

## Offene Fragen / Risiken
- Animierte Zeiger nur mit erstem Bild; monochromes „Invertieren“ (I-Balken) wird schwarz
  gezeichnet, der Rand hält es sichtbar.
- Kontrast/Helligkeit/Glättung bisher nur in `settings.json` (UI in M7).
