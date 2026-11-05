# M12 – Farbfilter pro Fenster

**Ziel:** Helle Fenster werden automatisch invertiert, dunkle bleiben dunkel – pro Fenster, ab Werk
an, auch ohne Zoom. Der Nutzer korrigiert falsche Einstufungen mit einem Kürzel oder auf einer
Detailseite; seine Wahl gilt dauerhaft für App + Fensterart.
**Demo:** Word (hell) und Windows Terminal (dunkel) nebeneinander, kein Zoom → nur Word ist
invertiert. `Strg+Alt+C` invertiert den ganzen Monitor → Word bleibt dunkel (nicht doppelt), das
Terminal wird hell. Eigenes Kürzel auf einem falsch erkannten Fenster → Wahl bleibt nach Neustart.
**Voraussetzungen:** M5 (Filter, Matrizen), M7 (Einstellungen), M11 (Telemetrie-Pflicht). Vor dem
Tag `v1.0.0`.
→ [ADR-0020](../adr/0020-filter-pro-fenster.md)

## Entscheidungen (Grilling 2026-11-05)
| Thema | Entscheidung |
|---|---|
| Bereich | nur die sichtbare Fläche des Fensters |
| Einstufung | jedes Fenster (hell und dunkel) einmal beim Öffnen: 3 Messungen in ~1 s, 2 von 3, max. 2 s |
| Danach | keine Messung mehr bis zum Schließen, auch nicht bei Fokus |
| Schwelle | hell = ≥ 60 % der Fläche mit Leuchtdichte ≥ 80 %; „Empfindlichkeit“ niedrig/mittel/hoch, Prozent unter „Erweitert“ |
| Nutzerwahl | Vorrang, gemerkt an App + Fensterart (Programm + Fensterklasse, kein Titel) |
| Wahl je Eintrag | Automatisch · Keiner · jeder vorhandene Filter; Automatik-Filter ab Werk „Invertiert“ |
| Monitorfilter | bleibt Standardweg (`Strg+Alt+C`); eigenständig invertierte Fenster werden herausgerechnet (nie doppelt) |
| Reihenfolge | Nutzerwahl → Automatik → Monitorfilter; Kontrast/Helligkeit für alles |
| Popups | Menüs/Tooltips/Aufklapplisten erben vom Besitzer; Dialoge werden gemessen |
| Ausnahmen | Medien/Bildbearbeitung vorbelegt „Keiner“; Windows-Oberfläche nie; Vollbild **nicht** ausgenommen |
| Aktiv | immer, auch ohne Zoom; nichts zu filtern → Overlay pausiert |
| Bedienung | Detailseite von der Farbfilter-Seite; Kürzel ab Werk unbelegt |
| Budget | ≤ 3 % CPU, Bildrate = Monitor-Hz, ≤ 1 Frame, kein sichtbarer Versatz beim Verschieben |

## Aufgaben
### 1 · Spike (Risiko zuerst)
- [ ] Fensterbereiche pro Frame: sichtbare Top-Level-Fenster je Monitor in Z-Reihenfolge, Verdeckung
      herausgerechnet; Quelle (WinEvents `EVENT_OBJECT_LOCATIONCHANGE`/`SHOW`/`HIDE`/`REORDER` +
      Abfrage der Rechtecke) ohne Allokation im Render-Thread; Versatz beim Ziehen messen
- [ ] Shader: Matrix je Bereich (Limit z. B. 16 Rechtecke im Constant Buffer); Verhalten bei mehr
      Bereichen festlegen (z. B. kleinste zusammenfassen, Rest Monitorfilter)
- [ ] Helligkeitsmessung auf der GPU: verkleinertes Bild je Fensterbereich, Anteil heller Pixel;
      Kosten pro Messung
- [ ] Pause: Overlay zeichnet nicht, wenn kein Bereich gefiltert wird; Aufnahme nur für Prüfungen
- [ ] Popups erkennen (Menü-, Tooltip-, Aufklappklassen, Besitzerfenster) und DPI/mehrere Monitore
- [ ] Budget messen (1440p, Onboard-Grafik): CPU, Bildrate, Verzögerung
- [ ] Ergebnis in `docs/kb/rendering-pipeline.md`; bei Fehlschlag Rücksprache (Plan B: ganzer Monitor
      nach aktivem Fenster)

### 2 · Core-Logik
- [ ] `WindowKind` (Programm + Fensterklasse), `WindowFilterRule` (Automatisch/Keiner/Filter),
      `WindowFilterRules` mit Ausnahmen ab Werk
- [ ] `BrightnessClassifier`: Schwelle aus Empfindlichkeit/Prozent, 3 Messungen, 2 von 3, Abbruch
      nach 2 s
- [ ] `WindowFilterResolver`: je Fenster Nutzerwahl → Automatik → Monitorfilter; Herausrechnen bei
      invertiertem Monitor; Popups erben
- [ ] Zustands-Snapshot für den Render-Thread (unveränderlich, atomarer Tausch, keine Allokation)
- [ ] Unit-Tests für alle Regeln inkl. „nie doppelt invertiert“ und Benchmark `0 B`

### 3 · Platform & Rendering
- [ ] Platform: Fensterquelle (Rechtecke, Z-Reihenfolge, Klasse, Programm, Besitzer, Popup-Art),
      Windows-Oberfläche erkennen
- [ ] Rendering: Bereiche + Matrizen im Shader, GPU-Messung, Pause; `WDA_EXCLUDEFROMCAPTURE` bleibt
- [ ] Telemetrie (ADR-0019): Einstufungen je Prozess (hell/dunkel/unklar), Dauer der Prüfung,
      Zahl der Bereiche, Pausenanteil – ohne Fenstertitel

### 4 · Einstellungen & Bedienung
- [ ] Settings: Automatik an (ab Werk), Automatik-Filter, Empfindlichkeit/Prozent, Regeln je
      App + Fensterart (JSON, Source Generator), Profile übernehmen sie
- [ ] Farbfilter-Seite: Karte „Filter pro Fenster“ → Detailseite mit Liste (App, Fensterart, Wahl),
      Ändern, Löschen, „Alle zurücksetzen“, Empfindlichkeit, „Erweitert“
- [ ] Kürzel „Filter für aktives Fenster umschalten“ (ab Werk unbelegt), merkt die Wahl, kurze
      Statusanzeige
- [ ] Texte Deutsch/Englisch, vollständig per Tastatur und Bildschirmleser bedienbar
- [ ] Sichere Bildschirme (M10): Verhalten festlegen (vermutlich Automatik aus)

## Akzeptanzkriterien
- [ ] Helles und dunkles Fenster nebeneinander: nur das helle invertiert, ohne Zoom
- [ ] Monitor invertiert: eigenständig invertiertes Fenster nicht doppelt invertiert (Test + Sicht)
- [ ] Menü in invertierter App blitzt nicht weiß auf
- [ ] Nutzerwahl per Kürzel bleibt nach Neustart für App + Fensterart erhalten
- [ ] Medien-Apps und Windows-Oberfläche werden ab Werk nicht gefiltert
- [ ] Nur dunkle Fenster sichtbar → Overlay pausiert (keine Aufnahme, kein Zeichnen)
- [ ] Budget eingehalten (≤ 3 % CPU, Monitor-Hz, ≤ 1 Frame, kein Versatz beim Verschieben)
- [ ] Render-Loop-Benchmark `0 B`; Architekturtests grün; Telemetrie im Katalog

## Offene Fragen / Risiken
- Fensterbereiche ohne Versatz und ohne Allokation pro Frame – Kern des Spikes.
- Limit der Bereiche im Shader; viele kleine Fenster.
- Overlay im Dauerbetrieb bei hellen Fenstern: Verzögerung, Akku, Spiele/Anti-Cheat.
- Vollbild-Anwendungen nicht ausgenommen: Spiele/Präsentationen in nicht gelisteten Apps.
- Einstufung nur beim Öffnen: spätere Hell/Dunkel-Wechsel und Ladezeiten > 2 s bleiben falsch.
- Chromium/Electron: eine Fensterklasse für alle Fenster → eine Regel für die ganze App.
- 1.0 verschiebt sich um M9 + M11 + M12.
