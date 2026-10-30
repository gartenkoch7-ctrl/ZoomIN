# M5 – Farbfilter & Hervorhebung

**Ziel:** Bessere Lesbarkeit durch Farbfilter, Kantenglättung und einen gut sichtbaren
Mauszeiger – alles im selben GPU-Durchgang.
**Demo:** `Win+Alt+C` invertiert die Farben; Text bleibt bei 8× gut lesbar;
großer gelber Zeiger mit Suchring.
**Voraussetzungen:** M3.

## Aufgaben

### 1. Farbfilter (`Core/Filters/`)
- [ ] `ColorMatrix` (4×4 + Offset) mit Kombination mehrerer Filter
- [ ] Voreinstellungen: Invertiert, Graustufen, Graustufen invertiert, Gelb auf Schwarz,
      Schwarz auf Gelb, Blau auf Weiß (eigene Werte, keine fremden Paletten übernehmen)
- [ ] Kontrast, Helligkeit, Sättigung als Parameter
- [ ] „Helligkeit erhaltend invertieren“ (Farbton bleibt) als Option
- [ ] Unit-Tests: bekannte Eingangsfarben → erwartete Ausgabe

### 2. Shader (`Rendering/Shaders/`)
- [ ] Filter-Konstanten im Constant Buffer, nur bei Änderung hochladen
- [ ] Sampling-Modi: bilinear, bikubisch, Kantenschärfung für Text
      (Vergleichsbilder in `docs/kb/` beschreiben, nicht SuperNova abbilden)
- [ ] GPU-Zeit messen (Onboard-Grafik, 4K) → Budget aus `performance.md` einhalten

### 3. Vergrößerter Mauszeiger (`Rendering/Cursor/`)
- [ ] Zeigerform aus `GetCursorInfo`/`GetIconInfoEx` → Textur, Cache pro `HCURSOR`
      (inkl. monochrome und animierte Zeiger)
- [ ] Zeichnen mit neuester Position direkt vor `Present`
- [ ] Systemzeiger ausblenden, solange Zoom aktiv – **Entscheidung als ADR-0008**:
      `MagShowSystemCursor` vs. `SetSystemCursor`; Absturzsicherheit (Zeiger muss immer
      zurückkommen)
- [ ] Optionen: Größe, Farbe, Umrandung, Suchring (bei Strg / bei Bewegung)

### 4. Fokus-/Caret-Marker (Vorbereitung)
- [ ] Shader kann Rechtecke/Linien mit Farbe zeichnen; Daten kommen in M6

## Akzeptanzkriterien
- [ ] Alle Filter in allen Modi, auf allen Monitoren, live umschaltbar
- [ ] Frame-Budget mit allen Filtern eingehalten (Messung in `performance.md`)
- [ ] Zeiger korrekt für Pfeil, I-Beam, Hand, Sanduhr (animiert), Resize-Pfeile
- [ ] Nach Absturz (Prozess abschießen) ist der Systemzeiger wieder sichtbar
- [ ] Filter und Zeiger in Teams-Aufnahme unsichtbar

## Offene Fragen / Risiken
- Farbfilter auf HDR-Inhalten (siehe M3).
- Die Windows-Farbfilter (Einstellungen > Barrierefreiheit) können parallel aktiv sein → Hinweis in der UI.
