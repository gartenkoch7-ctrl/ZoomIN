# M15 – Anpassender Mauszeiger

**Ziel:** Ein Mauszeiger, der seine Farbe nach dem Hintergrund richtet – wie die Windows-Zeigerart
„Invertiert“, aber ohne deren Schwäche auf Grau und mit frei wählbarem Farbpaar. Auf dunklem Grund
nimmt der Zeiger die eine Farbe (Standard Weiß), auf hellem die andere (Standard Schwarz); der Umriss
nimmt jeweils die Gegenfarbe.

**Demo:**
- Farbmodus „Passt sich dem Hintergrund an“ wählen. Den Zeiger über eine Schwarz/Weiß-Kante führen
  (z. B. dunkle Taskleiste → weißes Dokument): Die Teile des Zeigers über Schwarz sind weiß mit
  schwarzem Umriss, die über Weiß schwarz mit weißem Umriss – ohne graue Zwischentöne.
- Gleiches mit und ohne Zoom, in Vollbild, Lupe und Angedockt, mit „Invertiert (intelligent)“.
- Farbpaar Gelb/Blau wählen, Schwelle verschieben: Der Wechsel folgt sichtbar dem Regler.

**Voraussetzungen:** M14 gemergt (CursorPass mit `End` je Ebene, `ConstantUpload<T>`, Leerlauf-Frames).
→ [ADR-0026](../adr/0026-anpassender-zeiger-mit-hintergrund-kopie.md)

## Entscheidungen (Grilling 2026-11-08)
| Thema | Entscheidung |
|---|---|
| Prinzip | **Zwei Farben nach Helligkeit darunter**, keine echte Pixel-Invertierung (die wird auf Mittelgrau grau und unsichtbar, kein Farbpaar möglich) |
| Auflösung | **pro Pixel, geglättet:** jeder Zeigerpixel richtet sich nach seinem Umfeld (≈ 6 px weich), damit Schrift den Zeiger nicht sprenkelt |
| Umriss | **Gegenfarbe:** heller Teil des Zeigerbilds = Körper → Kontrastfarbe zum Hintergrund; dunkler Teil = Umriss → die andere Farbe des Paars |
| Umschalten | **Regler „Umschalten ab Helligkeit“** (Standard 50 %), **harter Wechsel** ohne Mischton (nur ≈ 1 px Kantenglättung) |
| Wirkung | **mit und ohne Zoom** – ZoomIN zeichnet den Zeiger dann auch bei 1× selbst |
| Farbmodi | **vierter Wert** in `PointerColorMode` (`Adaptive`), schließt Einfärben/Invertiert aus; alte Einstellungen bleiben gültig |
| Farbfilter | Entscheidung nach dem, **was man sieht**: fertiges Bild (Filter, Fensterfilter, intelligent) samt Desktop darunter |
| Kontrastrand | bleibt **frei wählbar** wie bisher, unabhängig vom Modus |
| Bunte Zeiger | **alle Zeiger** werden umgefärbt (helle Teile Körper, dunkle Umriss), auch Warte-Kreis und App-Zeiger; XOR-Pixel („Bildschirm invertieren“) werden Körper |
| Lupe/Angedockt | **überall anpassend:** außerhalb des Zoombereichs zeichnet ZoomIN den anpassenden Zeiger wie bei 1× an der echten Position |
| Einstellungen | zwei Farbwähler („auf dunklem Grund“, „auf hellem Grund“), Regler Schwelle, Knopf „Farben tauschen“; Vorschau zeigt den Zeiger über einer Hell/Dunkel-Kante |

## Umsetzung

### Schritt 1 – Core: Modell und Regeln
- `PointerColorMode.Adaptive`; `PointerStyle`: `AdaptiveDarkColor` (auf dunklem Grund, Standard
  Weiß), `AdaptiveLightColor` (auf hellem Grund, Standard Schwarz), `AdaptiveThreshold` (0,5;
  `Normalized` begrenzt 0,1–0,9). JSON-Source-Generator um die Felder ergänzen.
- `DrawsPointer`: `Adaptive` ≠ Original → zeichnet schon heute bei 1×; neu `NeedsDesktopUnzoomed`
  (Adaptive braucht die Desktop-Kopie auch ohne Zoom/Filter).
- `PointerPreview.Colored` bekommt dieselbe Regel wie der Shader (Körper/Umriss nach Zeiger-Luma,
  Farbe nach Hintergrund-Luma gegen Schwelle) – eine reine Funktion, testbar.
- Tests: Normalisierung, JSON-Rundreise mit alten Dateien (ohne neue Felder), Vorschau-Regel an
  Schwarz, Weiß, Grau knapp über/unter der Schwelle, bunte und halbtransparente Pixel.

### Schritt 2 – Rendering: Hintergrund unter dem Zeiger
- **Hintergrund-Kopie:** vor dem Zeiger-Durchgang ein kleiner Ausschnitt (Zeigergröße + Rand,
  höchstens 256 × 256) per `CopySubresourceRegion` aus dem Back Buffer **und** aus der
  Desktop-Kopie in zwei feste Texturen (angelegt beim Pipeline-Start, wiederverwendet).
- Was man sieht = Back Buffer (vormultipliziert) + Desktop · (1 − α). Damit stimmt es bei 1× mit
  durchsichtigem Overlay, außerhalb der Lupe und über dem fertig gefilterten Zoom.
- **Glättung:** Im Shader 4 bilineare Proben im Abstand von 3 px (je Mittel aus 4 Pixeln → Umfeld
  ≈ 6 px) – kein eigener Weichzeichner-Durchgang.
- `Cursor.hlsl`: neuer Modus `Tint.w = 3`; `CursorConstants` bekommt Farbe A, Farbe B, Schwelle und
  Lage des Hintergrund-Ausschnitts (Bildschirm → Ausschnitt-UV). Harte Schwelle mit
  `smoothstep` über ≈ 1 px (`fwidth`).
- Der Zeiger wird **gespiegelt** platziert (`PointerPlacement`): Die Proben laufen in
  Bildschirmkoordinaten, nicht in Zeigerbild-Koordinaten.

### Schritt 3 – Rendering: Wann ZoomIN zeichnet
- `CursorLayer`: Im Modus Adaptive zeichnet ZoomIN den Zeiger auch außerhalb von `frame.Source`
  (Lupe/Angedockt) an der echten Position; der Systemzeiger bleibt dann überall verborgen.
- Bei 1× ohne Filter läuft die Duplication **nur auf dem Monitor mit der Maus** (ADR-0026).
  Andere Monitore schlafen wie bisher.
- Leerlauf (M14): neuer Frame nur, wenn sich der Zeiger bewegt oder die Duplication einen neuen
  Desktop-Frame meldet, der den Ausschnitt um den Zeiger berührt (Dirty-Rects schneiden).
- Allokationstest (`CursorPassAllocationTests` auf WARP): 0 B pro Frame im Modus Adaptive.

### Schritt 4 – App: Mauszeiger-Seite
- Farbmodus-Auswahl um „Passt sich dem Hintergrund an“ erweitert.
- Nur in diesem Modus sichtbar: zwei `ColorChooser` (auf dunklem / hellem Grund), `ValueSlider`
  „Umschalten ab Helligkeit“ (10–90 %), Knopf „Farben tauschen“.
- Vorschau: Zeiger über einer Hell/Dunkel-Kante (zusätzlich zu hell/dunkel).
- Texte Deutsch/Englisch in `TextsGermanCards`/`TextsEnglishCards`. `PointerPage.xaml` (158
  Zeilen) bleibt unter 250, sonst die Karte als eigenes Steuerelement auslagern.

### Schritt 5 – Telemetrie, Messung, KB
- **Log** (Rendering, 3008, Info, je Pipeline einmal): „Adaptive pointer on {Monitor} keeps the
  desktop copy running without zoom“ – beim Wechsel in den Dauer-Capture bei 1×.
- **Messwert:** `zoomin.render.gpu_time` bekommt den Tag-Wert `zoomin.pass` = `cursor` (Instrument
  und Tags beim Start angelegt, Abfragen nur bei Abonnent wie bisher).
- **Keine** Logs/Activities pro Frame; Einstellungsänderungen laufen über das bestehende
  Einstellungs-Logging.
- **Budget:** Zeiger-Durchgang inkl. beider Kopien ≤ **0,05 ms** GPU auf dem Dev-PC (1440p,
  Zeiger 4×), hochgerechnet ≤ 0,5 ms Onboard; CPU pro Frame unverändert < 1 ms, 0 B.
- **Leerlauf bei 1×** messen: CPU/GPU mit ruhender Maus auf ruhendem Desktop (Ziel: wie M14 im
  Zoom-Leerlauf, nahe null) und beim Bewegen.
- KB: `rendering-pipeline.md` (Hintergrund-Kopie, Modus 3), `performance.md` (Messprotokoll),
  `telemetrie.md` (3008, Tag `cursor`), `eingabe-und-tracking.md` (Zeiger bei 1×).

## Akzeptanzkriterien
- [ ] Zeiger über Schwarz → Körper Farbe A, Umriss Farbe B; über Weiß umgekehrt; über Grau knapp
      unter/über der Schwelle sauber eine der beiden Farben, nie ein Mischton.
- [ ] Zeiger halb über einer Kante: beide Hälften kontrastreich; über Fließtext nicht gesprenkelt.
- [ ] Funktioniert mit 1×, Vollbild-Zoom, Lupe, Angedockt; auf jedem Monitor.
- [ ] Mit Filter „Invertiert“ und „Invertiert (intelligent)“ richtet er sich nach dem gefilterten Bild.
- [ ] Farbpaar, Schwelle, Tausch-Knopf wirken sofort; Vorschau zeigt dasselbe wie der Shader.
- [ ] Alte `settings.json` ohne die neuen Felder lädt unverändert.
- [ ] Render-Loop 0 B; GPU-Budget eingehalten und im Messprotokoll eingetragen.
- [ ] Overlay bleibt aus Aufnahmen ausgeschlossen (keine neuen Fenster).
- [ ] Sichtprüfung durch den Nutzer (das Overlay ist aus Screenshots ausgenommen; vorher
      Debug-Schnappschuss, der den Back Buffer samt Zeiger enthält).

## Offene Fragen / Risiken
- **Back Buffer als Kopierquelle:** Flip-Model-Back-Buffer müssen sich per
  `CopySubresourceRegion` lesen lassen (DXGI_USAGE_RENDER_TARGET_OUTPUT reicht laut Doku; auf
  WARP und echter GPU prüfen). Fallback: Ausschnitt vor dem Zeiger-Durchgang über eine
  Zwischen-Textur rendern.
- **Dauer-Capture bei 1×:** bricht die M14-Regel „Leerlauf: Capture gestoppt“ für diesen Modus
  (ADR-0026). Messen, ob ruhender Desktop wirklich nahe null bleibt.
- **Zeiger-Bewegung schneller als Desktop-Frames:** Der Hintergrund kommt aus der letzten
  Desktop-Kopie; bei schnellem Ziehen über frisch geänderte Inhalte kann die Farbe einen Frame
  nachhängen – akzeptiert.
- **Sichere Bildschirme (M10):** Die SYSTEM-Instanz zeichnet den Zeiger ebenfalls; gleiche Regeln,
  dort Sichtprüfung nachholen.
- **Clean-Room:** Vorbild ist die Windows-Zeigerart (Microsoft, öffentliche Einstellung), nicht
  SuperNova; keine Beobachtung aus SuperNova nötig.
