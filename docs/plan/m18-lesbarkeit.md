# M18 – Lesbarkeit

**Ziel:** Text und Markierungen werden besser lesbar. Dazu gehören schärfere und kräftigere Schrift,
eigene Farbschemata (auch bei Farbsehschwäche) und ein Textcursor, den man sofort findet. Die
Markierungen erscheinen nur dann, wenn man sie braucht.
**Demo:**
- 8× auf einer Webseite mit dünner Schrift: „Fett“ auf 1 px, „Schärfe“ auf 50 % → Text deutlich
  kräftiger.
- Eigenes Schema „Hellgrün auf Dunkelblau“ anlegen → erscheint in der Filterliste, bei
  `Strg+Alt+Umschalt+C` und in den Regeln pro Fenster.
- Textcursor als Keile in Gelb, wächst mit dem Zoom. Fadenkreuz nur bei gehaltener rechter
  Umschalttaste.

**Voraussetzungen:** M17 in `develop` gemergt. Ergebnis des D1-Spikes liegt vor. Release als **1.2**.
**Herkunft:** [feature-priorisierung.md](../kb/feature-priorisierung.md) (Bündel „Lesbarkeit“),
Grilling 2026-11-11.

## Features
| ID | Feature | Plan | Größe |
|---|---|---|---|
| D1 | Schärfen/Fetter (nur wenn der Spike besteht) | [d1-schaerfen-fetter.md](features/d1-schaerfen-fetter.md) | M |
| C1–C3 | Eigene Farbschemata | [c-eigene-farbschemata.md](features/c-eigene-farbschemata.md) | M |
| E2 | Caret-Formen | [e2-caret-formen.md](features/e2-caret-formen.md) | S |
| E1 | Anzeige-Bedingungen für Markierungen | [e1-anzeige-bedingungen.md](features/e1-anzeige-bedingungen.md) | S |

## Gemeinsame Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| D1-Grenze | ≤ 0,05 ms Zusatz auf RTX 3060 Ti bei 1080p (hochgerechnet ≤ 0,5 ms Onboard), keine störenden Halos mit Invertieren; sonst fällt D1 weg |
| Werkswerte | Schärfe/Fett aus; Caret-Balken wie heute; Markierungen „immer“ |
| Profile | Auswahl von Schärfe/Fett, Schema, Caret-Form, Anzeige-Bedingung gehört zum Profil; Schema-Definitionen und Haltetaste global |

## Parallelisierung
Integrationsbranch **`feat/m18-lesbarkeit`** (von `develop` nach M17). Regeln: [AGENTS.md §11](../../AGENTS.md).

```
Spur A: D1 Schärfen/Fetter (nur bei bestandenem Spike) ─┐
Spur B: C Farbschemata (Core zuerst, dann Shader/UI) ────┼─► Integration ─► Review ─► develop (mit Freigabe)
Spur C: E2 Caret-Formen ─► E1 Anzeige-Bedingungen ───────┘
```

| Spur | Inhalt | Branch | Start | Wartet auf | EventIds |
|---|---|---|---|---|---|
| A | D1 | `feat/m18-d1-schaerfen-fetter` | sofort | bestandener Spike | App 1600–1619, Rendering 3200–3219 |
| B | C1–C3 | `feat/m18-c-farbschemata` | sofort | – | App 1620–1639, Rendering 3220–3239 |
| C | E2, danach E1 | `feat/m18-e-markierungen` | sofort | – | E2: App 1640–1649, Rendering 3240–3249 · E1: App 1650–1659, Platform 2950–2959, Rendering 3250–3259 |

**Merge-Reihenfolge (tatsächlich):** C → D1 → E, also Spur B → A → C.
- Der D1-Branch wurde auf C aufgebaut (`Magnify.hlsl`: `PSEnhanced` teilt `Shade` mit Palette und
  Ersetzen) und enthält C schon. Darum kam C zuerst.
- Ursprünglich geplant war A → B → C.

**Exklusiv:**
- `DesktopLayer.cs` (Pass-Struktur): Spur A
- `ColorFilter`/`ColorFilters`/`WindowPalette`, `Magnify.hlsl` (Palette, Ersetzen): Spur B
- `HighlightLayer.cs`, `Cursor.hlsl`: Spur C

E2 und E1 laufen in **einer** Spur hintereinander, weil beide diese Dateien ändern.

**Hotspots** (nur ergänzend):
- `AppSettings.cs`, `ProfileFields.cs`, `EffectsMapping.cs`, `TrackingMapping.cs`
- `Texts*.cs`
- `docs/kb/telemetrie.md`
- Seiten Farbfilter, Mauszeiger, Fokus & Verfolgung

**Früher Start erlaubt:**
- Spur B mit dem Core-Teil (Filter datengetrieben) und Spur C dürfen schon während M17 beginnen.
- Bedingung: Keine der dort genannten Exklusiv-Dateien wird gerade bearbeitet.
- Gemergt wird erst in den M18-Integrationsbranch.

**Nutzen:** etwa 2–3 Wochen statt 4–5 hintereinander.

## Aufgaben (Integration)
- [x] D1-Spike auswerten. Bestanden → Spur A, sonst D1 im Plan als „verworfen“ mit Begründung
- [x] Integrationsbranch anlegen (von `feat/m17-ruhige-ansicht`, weil M17 noch nicht in `develop` ist), Spuren starten (Worktree je Spur)
- [x] Mergen C → D1 → E, nach jedem Merge Build + Tests + WARP-Shadertests
- [ ] GPU-Budget aller neuen Schritte zusammen messen (Frame ≤ 2 ms Onboard, hochgerechnet) – braucht echte GPU (`ZOOMIN_GPU_TIMING=1`)
- [x] Gesamt-Review (§9): vier Runden, Rest siehe „angenommene Kompromisse“
- [x] Wechselwirkungen prüfen (Tests):
  - Schärfen mit Invertieren, intelligentem Invertieren (D1-Spur) und eigenen Schemata (`EnhanceSchemeTests`)
  - Caret-Form mit den Anzeige-Bedingungen und der Übersicht (`OverviewHighlightTests`)
  - Haltetaste gegen Esc der Übersicht (`HoldKeyCodesTests`)
  - Profile aus 1.0 und mit M18-Feldern (`JsonProfileStoreTests`), Einstellungen mit Custom1–4 (`JsonSettingsStoreTests`)
- [x] Benchmarks (`--job short --filter *`): alle 0 B
- [ ] Sichtprüfung per KI-Debug-Schnappschuss, danach Nutzer
- [ ] Release-Notes 1.2, Status hier und in der Roadmap; Merge nach `develop` nur mit Freigabe

## Integration: Konflikte und Auflösung (2026-11-12)
- `CenterMouse` aus Spur E entfällt. Es gilt M17s `MouseFollow` + `MouseMargin` (Schema 2). Caret-Form
  und Bedingungen stehen daneben in `TrackingOptions`, `TrackingMapping`, `AppSettings` und `ProfileFields`.
- M17 hat `RenderSession` aufgeteilt. Die Änderungen der Spuren am Konstruktor (`PassCostProbe`,
  `EnhanceCost`, `HighlightMetrics`) stehen jetzt in `RenderResources`. Überblendung (E1) und
  Übersichtsrahmen (B2) halten beide das Zeichnen am Laufen.
- `AppSettings` war über 250 Zeilen. Die gemeinsamen Eingabe-Einstellungen stehen jetzt im
  Basis-Record `InputSettings`, das JSON-Format bleibt gleich.
- Texte: Für die Maus gelten die Texte von M17, für die Caret-Markierung die Formulierung von E.
- EventIds: keine Doppelbelegung (App 1620–1626, 1640, 1650–1651; Platform 2950–2954;
  Rendering 3200–3201, 3220–3221).

## Integration: angenommene Kompromisse
- **Gelöschtes Schema, Profil nicht umgeschrieben:** Ist eine Profildatei beim Löschen gesperrt
  (Log 1625), zeigt sie weiter auf den Platz. Ein später neu angelegtes Schema auf diesem Platz
  erscheint dann in dem Profil. Dasselbe gilt für exportierte Profile, denn Schema-Definitionen
  sind global. Auch ein Profilwechsel genau während des Löschens kann den Platz zurückbringen.
  Selten, darum hingenommen.
- **Strg als Haltetaste:** Ein Halten bei gedrückter Umschalttaste meldet kein „Strg allein“.
  Ein echtes Strg-Tippen innerhalb von 500 ms danach kann trotzdem verschluckt werden.
  Der Abgleich zweier Hooks bleibt so, wie Spur E ihn gebaut hat.
- **Zwei Uhren:** `MarkerActivity` nutzt `TimeProvider`, der Render-Thread `Stopwatch`. Das passt nur
  mit `TimeProvider.System`; so steht es am Konstruktor.
- **Angedockt bei kleiner Zoomstufe:** Die Ansicht kann je Achse anders vergrößern. `FrameMapping`
  rechnet seit M6 mit der waagrechten Achse, Caret-Formen also auch. Das ist kein M18-Fehler.
- **Profilwechsel:** `EffectsModel.Replace` normalisiert die gemeinsamen Schemata bei jedem
  Profilwechsel neu. Das kostet etwas Zeit, gilt aber nicht für den Render-Loop.
- **Schema-Editor:** Jeder Tastendruck im Namen veröffentlicht neue Effekte. Das ist
  UI-Rate, kein Bild ändert sich.
- **Haltetaste:** Umschalt rechts löst nach 8 s die Windows-Anschlagverzögerung aus. Alt allein
  öffnet Menüs. Der Hook läuft auch bei Zoom aus, solange eine Bedingung „Haltetaste“ ist.
- **Überblendung:** Sie läuft auf allen Monitoren, auch ohne Caret dort. Das sind einige
  Zusatz-Frames je Wechsel von Ruhe und Bewegung.
- **Druckmoment:** Zwischen Hook-„Down“ und dem aktualisierten Tastenzustand kann der 400-ms-Timer ein
  Halten beenden. Das Fenster dafür ist Mikrosekunden groß.
- **Ältere Muster, nicht M18:** `TrayMenuSync` nutzt die Fassade `Text` statt `ILocalizer`. Kommt mit
  M14 Schritt 6.
- **Weitere Punkte:** Die Caret-Vorschau rechnet in XAML-Einheiten. Testhilfen stehen in
  `MarkerAlphas` und `EnhanceCost`. `TextEnhancement.WorksWith` wird doppelt geprüft.
- **Duplikate aus den Spuren:**
  - `TextEnhance.hlsli` kopiert die Catmull-Rom-Abtastung aus `Magnify.hlsl`. Ein Kommentar
    verlangt, beide gemeinsam zu ändern.
  - `CustomSchemeService.SameScheme` vergleicht Schemata inhaltlich. Die Record-Gleichheit vergleicht
    die Ersetzungsliste nur als Referenz.
- **Ruhe und Bewegung:** Der Zustand zwischen „Halten beendet“ und „Strg tippen“ kann in einem
  Fenster von Mikrosekunden noch ein Tippen verschlucken (Runde 5 hat den häufigeren Fall behoben).
- Das Review lief, bis eine Runde keine Codeänderung mehr ergab.

## Offen für Menschen
- Sichtprüfung mit echter GPU:
  - Schärfe/Fett mit eigenem Schema
  - Caret-Formen bei 1×/8× und in der Übersicht
  - Überblendung der Bedingungen
- GPU-Budget aller M18-Schritte zusammen auf echter Hardware.
- App-Matrix für die Haltetaste: Alt-Menüs, Umschalt beim Großschreiben, Alt Gr.

## Akzeptanzkriterien
- [ ] Akzeptanzkriterien aller Feature-Pläne erfüllt
- [ ] Alte Einstellungen (Filter als Name gespeichert, Regeln pro Fenster) werden ohne Verlust übernommen
- [ ] Render-Loop `0 B`, Bildrate = Monitor-Hz mit allen neuen Effekten an
- [ ] Overlays weiter aus Aufnahmen ausgenommen

## Offene Fragen / Risiken
- Palette wächst von 10 auf 14 Einträge. Die Kosten der Bereichsschleife (64 Bereiche) müssen
  gemessen werden.
- Ohne echte Onboard-Grafik bleiben die GPU-Werte hochgerechnet. Hardware-Berichte nach 1.2
  auswerten.
