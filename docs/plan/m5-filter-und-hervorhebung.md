# M5 – Farbfilter & Hervorhebung

**Ziel:** Bessere Lesbarkeit durch Farbfilter, Kantenglättung und einen gut sichtbaren
Mauszeiger – alles im selben GPU-Durchgang.
**Demo:** `Strg+Alt+C` schaltet den Filter; Text bleibt bei 8× scharf; großer Zeiger;
`Strg+Alt+S` zeigt die Suchhilfe.
**Voraussetzungen:** M3.

## Entscheidungen (Grilling 2026-10-30)
- Filter im MVP: **Invertiert**, **Invertiert mit Farbtonerhalt**, **Graustufen** (normal +
  invertiert), **Zweifarb-Schemata** (Gelb/Schwarz, Schwarz/Gelb, Weiß/Blau)
- Standard-Glättung: **text-optimiert** (kantenschärfend); umschaltbar auf „weich“ und „Pixel“
- Zeiger **wächst mit dem Zoom**, begrenzt durch eine einstellbare **Maximalgröße**
- Suchhilfe: per **`Strg+Alt+S`** und optional automatisch bei Bewegung **nach Stillstand**

## Aufgaben

### 1. Farbfilter (`Core/Filters/`)
- [ ] `ColorMatrix` (4×4 + Offset), Kombination mehrerer Filter
- [ ] Voreinstellungen wie oben (eigene Werte, keine fremden Paletten)
- [ ] Farbtonerhaltende Invertierung (Helligkeit in z. B. HSL/OKLab invertieren)
- [ ] Kontrast, Helligkeit, Sättigung
- [ ] Unit-Tests: bekannte Eingangsfarben → erwartete Ausgabe

### 2. Shader (`Rendering/Shaders/`)
- [ ] Filter-Konstanten im Constant Buffer, nur bei Änderung hochladen
- [ ] Sampling: text-optimiert (z. B. geschärftes bikubisch), bilinear, Point
- [ ] Bei farbtonerhaltender Invertierung: Umrechnung im Shader, Budget prüfen
- [ ] WARP-Tests je Filter (Stichproben-Pixel)
- [ ] GPU-Zeit 4K unter Budget aus [performance.md](../kb/performance.md)

### 3. Vergrößerter Mauszeiger (`Rendering/Cursor/`)
- [ ] Zeigerform aus `GetCursorInfo`/`GetIconInfoEx` → Textur, Cache pro `HCURSOR`
      (monochrom, farbig, animiert)
- [ ] Größe = min(Zoom × Originalgröße, Maximum aus Settings)
- [ ] Zeichnen mit neuester Position direkt vor `Present`
- [ ] Systemzeiger ausblenden, solange Zoom aktiv – Verfahren als **ADR-0011**
      (`MagShowSystemCursor` vs. `SetSystemCursor`); Watchdog stellt ihn nach Absturz wieder her
- [ ] Optionen: Farbe, Umrandung

### 4. Suchhilfe
- [ ] Ring/Pulsieren um den Zeiger für ~1 s bei `Strg+Alt+S`
- [ ] Optional: automatisch, wenn die Maus nach > X s Stillstand bewegt wird (Standard aus)

### 5. Fokus-/Caret-Marker (Vorbereitung)
- [ ] Shader zeichnet Rechtecke/Linien; Daten kommen in M6

## Akzeptanzkriterien
- [ ] Alle Filter in allen Modi und Monitoren, live umschaltbar
- [ ] Frame-Budget mit allen Filtern eingehalten (60 + 144 Hz)
- [ ] Zeiger korrekt für Pfeil, I-Beam, Hand, Sanduhr (animiert), Resize-Pfeile
- [ ] Prozess abschießen → Systemzeiger wieder sichtbar
- [ ] Filter und Zeiger bei Freigabe unsichtbar

## Offene Fragen / Risiken
- Windows-Farbfilter (Barrierefreiheit) können parallel aktiv sein → Hinweis in der UI.
