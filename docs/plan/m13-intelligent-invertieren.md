# M13 – Intelligentes Invertieren

**Ziel:** Ein neuer Filter „Invertiert (intelligent)“ invertiert nur helle Flächen. Dunkle Leisten
und Bereiche bleiben dunkel, Fotos und Videos bleiben original – live in jedem Frame, mit weichem
Übergang und ohne Flackern.
**Demo:** Systemsteuerung im dunklen Windows-Design: Die helle Inhaltsfläche wird dunkel, die dunkle
Navigations- und Adressleiste darunter bleibt dunkel. Ein Foto im Browser bleibt in seinen
Originalfarben. Beim Scrollen springt nichts. Dasselbe gilt mit `Strg+Alt+C` auf dem ganzen Monitor
sowie in Lupe und Angedockt.
**Voraussetzungen:** M5 (Filter-Matrizen), M12 (Filter pro Fenster, Palette, ADR-0023). Pflicht vor
dem Tag `v1.0.0`.
→ [ADR-0024](../adr/0024-intelligent-invertieren.md)

## Entscheidungen (Grilling 2026-11-06)
| Thema | Entscheidung |
|---|---|
| Geltungsbereich | überall, wo dieser Filter gewählt ist: Filter pro Fenster, Monitorfilter (`Strg+Alt+C`), Lupe, Angedockt |
| Bedienung | eigener Filter „Invertiert (intelligent)“ / „Inverted (smart)“; das klassische „Invertiert“ bleibt |
| Prinzip | je Bildstelle entscheidet die Helligkeit der Umgebung, live in jedem Frame, mit weichem Übergang |
| Fotos/Videos | nicht automatisch invertiert: eine bunte, detailreiche Umgebung gilt als Foto und bleibt original |
| Farben | wo invertiert wird, bleibt der Farbton erhalten (wie „Invertiert, Farbton erhalten“) |
| Flackern | breiter weicher Bereich statt harter Schwelle, dazu zeitliche Glättung (~150 ms) |
| Fenster-Einstufung | bleibt (ADR-0020): sie entscheidet, welche Fenster gefiltert werden; innerhalb wirkt intelligent |
| Titelleiste | ADR-0023 bleibt als Ergänzung (Messung, Spalte, „Nie filtern“) |
| Zoom | Umgebung gemessen am Desktop-Bild (Quelle), nicht am vergrößerten Bild – nichts kippt beim Zoomen |
| Einstellungen | nur „Fotos und Videos nicht invertieren“ unter „Erweitert“ (ab Werk an); Schwelle, Umgebung, Glättung fest |
| Ab Werk | Automatik-Filter wird „intelligent“, wenn er noch auf dem alten Werkswert „Invertiert“ steht; Monitorfilter und App-Regeln bleiben |
| Budget | ≤ 1 ms zusätzliche GPU-Zeit pro Frame bei 1440p auf Onboard-Grafik; sonst Rückfall auf „Invertiert, Farbton erhalten“ (Log + Diagnose) |
| Messung | GPU-Zeitstempel auf der RTX 3060 Ti, hochgerechnet auf Onboard-Grafik; echte Onboard-Messung offen, bis ein Gerät da ist |
| Planung | eigener Meilenstein, Pflicht vor `v1.0.0` |

## Aufgaben
### 1 · Spike (Risiko zuerst)
- [x] Umgebungsbild: verkleinerte Kopie des Desktop-Bilds (je Monitor, z. B. 1/16 bis 1/32) mit
      Helligkeit und einem Maß für Farbigkeit/Detailreichtum; nur neu bei geändertem Desktop.
      Heute hat die Kopie keine Mip-Stufen und ist kein Render-Target (`DesktopDuplicator`) →
      eigener Verkleinerungs-Durchgang, Feature-Level 10_0 muss reichen
- [x] Gedächtnis-Textur für die zeitliche Glättung (~150 ms), unabhängig von der Bildrate
- [x] Shader: Entscheidung je Pixel aus dem Umgebungsbild (bilinear gelesen, weiche Schwelle),
      Foto-Schutz, Farbton erhalten; nur für Bereiche, deren Palette-Eintrag „intelligent“ ist
- [ ] Werte abstimmen: Umgebungsgröße, Schwelle und Breite des Übergangs, Foto-Kriterium, Glättung –
      an Systemsteuerung, Explorer, Browser mit Fotos, Word, Video (Sichtprüfung durch den Nutzer)
- [x] GPU-Zeit messen (Zeitstempel-Abfragen) auf der RTX bei 1080p, hochrechnen auf 1440p
      Onboard-Grafik; Grenze für den Rückfall festlegen
- [ ] Ergebnis in `docs/kb/rendering-pipeline.md` und `docs/kb/performance.md`; bei Fehlschlag
      Rücksprache (Plan B: Kachel-Entscheidung ohne Glättung, oder nur Fenster-/Monitorebene)

### 2 · Core
- [x] `ColorFilter.InvertedSmart` (am Ende des Enums) und Name/Texte; Palette wächst um einen
      Eintrag (`PALETTE_SIZE` im Shader und `WindowConstants` mitziehen)
- [x] „Intelligent“ ist keine Matrix: Palette-Eintrag mit Kennzeichen „adaptiv“ + Matrix für
      „Farbton erhalten“, Kontrast/Helligkeit weiter für alles
- [x] Settings: Schalter „Fotos und Videos nicht invertieren“ (ab Werk an); Migration des
      Automatik-Filters (nur alter Werkswert „Invertiert“ → „intelligent“)
- [ ] Unit-Tests (Palette, Migration, Settings-Rundreise) und Benchmark `0 B`

### 3 · Rendering
- [ ] Verkleinerungs-Durchgang und Gedächtnis-Textur je Monitor, beim Start/Resize angelegt,
      keine Allokation im Render-Loop; nur aktiv, wenn ein Bereich oder der Monitor „intelligent“ nutzt
- [x] Vollbild-Zoom, Lupe und Angedockt: Umgebung in Quell-Koordinaten (Desktop), auch im
      1×-Durchgang außerhalb der Lupe
- [x] Rückfall auf „Farbton erhalten“, wenn eine GPU-Funktion fehlt oder das Budget gerissen wird
- [ ] `WDA_EXCLUDEFROMCAPTURE` bleibt; WARP-Shadertests (dunkle Leiste bleibt, helle Fläche
      invertiert, Foto bleibt, weicher Übergang, Glättung über Frames)

### 4 · Telemetrie (ADR-0019)
- [x] Messwert: GPU-Zeit des Verkleinerungs-Durchgangs und des Shaders (Instrumente beim Start)
- [x] Log: Rückfall auf „Farbton erhalten“ mit Grund (GPU-Funktion/Budget), einmal je Sitzung
- [x] Katalog `docs/kb/telemetrie.md`, Diagnose-Seite zeigt „intelligent aktiv / Rückfall“

### 5 · Einstellungen & Bedienung
- [x] Filterlisten (Farbfilter-Seite, Automatik-Filter, Regeln je App) zeigen „Invertiert (intelligent)“
- [x] „Fotos und Videos nicht invertieren“ auf der Farbfilter-Seite unter „Anpassen“; Texte Deutsch/Englisch,
      Tastatur, Bildschirmleser (ToggleSwitch mit Kartenüberschrift)

### Stand Spike 2026-11-06 (Branch `feat/m13-intelligent-invertieren`)
- Läuft im Dev-Build: Filter „Invertiert (intelligent)“ wählbar für Monitor, Automatik-Filter und
  App-Regeln, in Liste und `Strg+Alt+Umschalt+C` direkt nach „Invertiert“ (`ColorFilters.DisplayOrder`).
- WARP-Tests: dunkle Leiste bleibt, Textseite invertiert, buntes Foto bleibt (ohne Schutz invertiert),
  nur im Fensterbereich, Glättung über Frames; 0 B im Render-Loop.
- GPU auf RTX 3060 Ti, 1440p: **+0,046 ms** pro Frame (Hochrechnung Onboard ≈ 0,5 ms).
- Werte (Schwellen, Umfeld 32 px, Glättung 50 ms) vorläufig; Abstimmung nach der Sichtprüfung.
- Migration erledigt (2026-11-06): Automatik-Filter ab Werk „intelligent“; alter Werkswert
  „Invertiert“ wird einmalig umgestellt (`AutoFilterRevision`, fehlt in alten Settings = 0). Wer
  danach selbst „Invertiert“ wählt, behält es. Anlass: Einstellungen-App wurde mit dem alten
  Automatik-Filter klassisch invertiert – der intelligente Filter kam gar nicht zum Zug.
- Erledigt 2026-11-06: Schalter „Fotos schützen“ (Settings + Farbfilter-Seite), Rückfall bei
  GPU-Funktion/Budget (`SmartInvertLayer`, `GpuStopwatch`), Status auf der Diagnose-Seite.
- Noch offen: Sichtprüfungen (Foto, Video, Scrollen, Lupe/Zoom, Rand an Fenstergrenzen), Werte
  feinabstimmen, Budget-Messung auf echter Onboard-Grafik.

### Debug-Schnappschuss 2026-11-06 (Entscheidung des Nutzers)
- [x] Knopf „Schnappschuss in 5 s“ auf der Diagnose-Seite: Original, Ergebnis, Gewichte und
      Info je Monitor als Dateien, damit die KI das gefilterte Bild auswerten kann (das Overlay
      fehlt in jeder Bildschirmaufnahme). Nur auf Knopfdruck, nur lokal, nie im Diagnosepaket.
- Anlass: In der Einstellungen-App wird eine dunkle Fläche unter der Titelleiste trotz
  „intelligent“ weiß – Ursache noch offen, wird mit dem Schnappschuss untersucht.

### Fix Hover-Saum 2026-11-06
- Fehler (Nutzer): Beim Hovern über Links in der Systemsteuerung wurde es grau und verschwommen.
  Ursache: bilinear gelesene Gewichte → halb invertierter Saum (16–32 px) an jeder Kante, dazu eine
  Kachel-Zuordnung, die über den Monitor driftete. Gelöst ohne den Nutzer: Original per Screenshot,
  Offline-Wiedergabe der echten Shader (`OfflineRenderTests`), binäre Kachel-Entscheidung mit
  Hintergrund-Luma + kantenerhaltende Hochskalierung → Übergang 1–2 px (KB rendering-pipeline).
- Vom Nutzer bestätigt 2026-11-06 (dev.115.m13): Hover in der Systemsteuerung sauber.
- Risiko: Kacheln kippen hart (nur zeitlich geglättet) – beim Scrollen über Mischflächen prüfen.

### Fix graue Höfe um Symbole 2026-11-06
- Fehler (Nutzer): Hover auf Link-Kästen in der Systemsteuerung „leicht grau“, Tooltip „komisch
  grau“. Selbst nachgestellt und offline wiedergegeben: halb als Foto geschützte Kacheln um bunte
  Symbole und blauen Linktext gaben graue Höfe. Foto-Test jetzt über 64 px und als Entscheidung.
- Tooltip konnte nicht nachgestellt werden (Windows zeigt bei simulierter Maus keinen); WARP-Test
  „dunkler Tooltip bleibt dunkel“ grün. Falls er noch grau ist: Schnappschuss mit Tooltip.
- Vom Nutzer bestätigt 2026-11-06 (dev.116.m13): Hover und Tooltip in der Systemsteuerung sauber.
- Budget-Hinweis: +0,083 ms auf der RTX, Hochrechnung Onboard ≈ 0,8 ms – nah am Budget.

## Akzeptanzkriterien
- [x] Systemsteuerung (dunkles Design): helle Fläche dunkel, dunkle Leisten bleiben dunkel – geprüft
      2026-11-06 vom Nutzer an der Einstellungen-App (dev.113.m13, Automatik-Filter intelligent)
- [ ] Foto im Browser bleibt original; ein Video läuft ohne Negativ-Look
- [ ] Scrollen einer Seite mit hellen und dunklen Teilen: kein Flackern, kein Springen
- [ ] Monitorfilter, Lupe und Angedockt verhalten sich gleich; Zoomen kippt keine Fläche
- [ ] Budget: ≤ 1 ms extra GPU (hochgerechnet; Onboard-Messung, sobald Gerät da); Monitor-Hz gehalten
- [ ] Rückfall greift und ist in Log und Diagnose sichtbar
- [ ] Render-Loop-Benchmark `0 B`; Architekturtests grün; Telemetrie im Katalog

## Offene Fragen / Risiken
- Review Spike: Das Umfeld (32 px) reicht über Fenstergrenzen. Neben einem dunklen Fenster kann
  der Rand eines hellen, intelligent gefilterten Fensters halb invertiert bleiben. Nach der
  Sichtprüfung entscheiden: Proben auf den eigenen Fensterbereich begrenzen (b1 im Umgebungs-Shader).
- Review Spike: Der Umgebungs-Durchgang läuft auch bei ruhendem Desktop (RTX ~0,03 ms). Er ließe
  sich überspringen, wenn kein neues Bild kam und die Glättung eingeschwungen ist.
- Downgrade: Ältere Versionen kennen `InvertedSmart` nicht. Ihre Settings-Datei gilt dann als
  defekt (Werkswerte, Sicherung `.broken`). Bei Versionswahl rückwärts (ADR-0014) beachten.
- Saum an Kanten zwischen hell und dunkel (Breite = Umgebungsgröße); bei hohem Zoom sichtbar breiter.
- Foto-Erkennung: helle, blasse Fotos werden evtl. invertiert; bunte Bedienflächen evtl. nicht.
- Text auf mittlerem Grau (z. B. Schaltflächen) liegt im Übergang und verliert Kontrast.
- Zeitliche Glättung zieht bei schnellem Inhaltswechsel kurz nach.
- Kein Onboard-Gerät zum Messen; Hochrechnung kann daneben liegen.
- 1.0 verschiebt sich um M13.
