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

**Merge-Reihenfolge:** A → B → C.
- D1 legt die Pass-Struktur in `DesktopLayer` fest.
- C passt danach nur den Inhalt der Konstantenpuffer und die Palette an.

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
- [ ] D1-Spike auswerten. Bestanden → Spur A, sonst D1 im Plan als „verworfen“ mit Begründung
- [ ] Integrationsbranch anlegen, Spuren starten (Worktree je Spur)
- [ ] Mergen A → B → C, nach jedem Merge Build + Tests + WARP-Shadertests
- [ ] GPU-Budget aller neuen Schritte zusammen messen (Frame ≤ 2 ms Onboard, hochgerechnet)
- [ ] Gesamt-Review (§9) bis ohne Befunde
- [ ] Wechselwirkungen prüfen:
  - Schärfen mit Invertieren, intelligentem Invertieren und eigenen Schemata
  - Caret-Form mit den Anzeige-Bedingungen
- [ ] Sichtprüfung per KI-Debug-Schnappschuss, danach Nutzer
- [ ] Release-Notes 1.2, Status hier und in der Roadmap; Merge nach `develop` nur mit Freigabe

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
