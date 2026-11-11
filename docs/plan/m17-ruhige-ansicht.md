# M17 – Ruhige Ansicht & Orientierung

**Ziel:** Das vergrößerte Bild bewegt sich nur noch, wenn es nötig ist. Man findet sich bei hoher
Stufe schnell zurecht und sieht beim Verschieben von Fenstern, wohin sie kommen.
**Demo:**
- 4× Vollbild. Maus in der Bildmitte bewegen → das Bild steht still. Erst am Randabstand (20 %)
  schiebt es sich mit.
- `Strg+Alt+O` → 1× mit Rahmen um den bisherigen Ausschnitt. Maus bewegen, erneut drücken → Zoom
  dort.
- Fenster an der Titelleiste ziehen → der Monitor geht auf 1×. Loslassen → er gleitet zurück auf 4×.

**Voraussetzungen:** v1.0.0 veröffentlicht. Release als **1.1**.
**Herkunft:** [feature-priorisierung.md](../kb/feature-priorisierung.md) (Bündel „Ruhige Ansicht &
Orientierung“), Grilling 2026-11-11.

## Features
| ID | Feature | Plan | Größe |
|---|---|---|---|
| A1 | Ruhige Mausverfolgung mit Randabstand | [a1-ruhige-mausverfolgung.md](features/a1-ruhige-mausverfolgung.md) | S–M |
| B2 | Übersicht auf Knopfdruck | [b2-uebersicht.md](features/b2-uebersicht.md) | S–M |
| G1 | Zoom beim Fenster-Verschieben | [g1-zoom-beim-verschieben.md](features/g1-zoom-beim-verschieben.md) | S–M |
| D1 | Spike Schärfen/Fetter (parallel, nicht Teil von 1.1) | [d1-schaerfen-fetter.md](features/d1-schaerfen-fetter.md) | S |

## Gemeinsame Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Zeitpunkt | nach v1.0.0, eigenes Release 1.1 |
| Gemeinsamer Baustein | „vorübergehende Zoomstufe“ pro Monitor: wird nicht in `ZoomLevels` geschrieben und nicht gespeichert, gleitet mit dem `ZoomAnimator` |
| Profile | Maus-Modus, Randabstand und Fenster-Verschieben gehören zum Profil; Kürzel gelten global |
| Seiten | A1 auf „Fokus & Verfolgung“, G1 auf „Zoom & Modi“, B2-Kürzel auf „Tastenkürzel“ |

## Parallelisierung
Integrationsbranch **`feat/m17-ruhige-ansicht`** (von `develop`). Regeln: [AGENTS.md §11](../../AGENTS.md).

```
Schritt 0 (Baustein) ──┬─► Spur B: B2 Übersicht ─────────┐
                       └─► Spur C: G1 Fenster-Verschieben ─┤
Spur A: A1 Randabstand (sofort) ──────────────────────────┼─► Integration ─► Review ─► develop (mit Freigabe)
Spur D: D1-Spike (sofort, eigener Branch von develop) ─────┴─► nur Ergebnis in KB/M18
```

| Spur | Inhalt | Branch | Start | Wartet auf | EventIds |
|---|---|---|---|---|---|
| 0 | Baustein „vorübergehende Zoomstufe“ in `ZoomModel` + Tests | `feat/m17-ruhige-ansicht` | sofort | – | – |
| A | A1 | `feat/m17-a1-randabstand` | sofort | – | App 1500–1519 |
| B | B2 | `feat/m17-b2-uebersicht` | nach Schritt 0 | Schritt 0 | App 1520–1539, Platform 2920–2939, Rendering 3100–3119 |
| C | G1 | `feat/m17-g1-fenster-verschieben` | nach Schritt 0 | Schritt 0 | App 1540–1559, Platform 2900–2919 |
| D | D1-Spike | `spike/d1-schaerfen-fetter` (von `develop`) | sofort | – | wird nicht gemergt |

**Merge-Reihenfolge in den Integrationsbranch:** A → B → C. Nach jedem Merge Build und Tests.
- **B nutzt den Anker-Modus aus A:** „hineinzoomen am Rahmen“ setzt den neuen Anker. Spur B
  arbeitet bis dahin gegen eine Schnittstelle. Sie holt den Integrationsbranch nach dem Merge von A
  herein und wird erst danach gemergt.
- **C beginnt mit einem Mini-Spike** (≈ 1 Tag, parallel zu Schritt 0): Lassen sich Verschieben und
  Größe ändern sicher unterscheiden? Dazu die App-Matrix.

**Hotspots** (nur ergänzend ändern, Konflikte löst die Integration):
- `AppSettings.cs`, `ProfileFields.cs`, `TrackingMapping.cs`, `ZoomMapping.cs`
- `HotkeyGesture.cs`, `HotkeyBindings.cs`
- `Texts{German,English}*.cs`
- `docs/kb/telemetrie.md`, `docs/kb/eingabe-und-tracking.md`

**Exklusiv:**
- `ZoomModel.cs`: nur Schritt 0. Spuren B und C nutzen den Baustein nur.
- `AnchorController.cs`, `ViewCentering.cs`: nur Spur A.

**Nutzen der Parallelisierung:** etwa 3 Wochen statt 5 hintereinander. A und D laufen ab Tag 1,
B und C nach Schritt 0 (≈ 1–2 Tage) gleichzeitig.

## Aufgaben (Integration)
- [x] Integrationsbranch anlegen, Schritt 0 umsetzen, Review-Schleife (§9), Commit, Push
      (`ZoomModel.Temporary` / `TemporaryZoom`, siehe [architektur.md](../kb/architektur.md#vorübergehende-zoomstufe-m17-temporaryzoom))
- [ ] Spuren A, D sofort starten; B, C nach Schritt 0 (Subagenten mit Worktree oder eigene Sitzungen)
- [ ] Spuren der Reihe nach mergen (A → B → C), nach jedem Merge `dotnet build` + `dotnet test`
- [ ] Gesamt-Review des ganzen Meilensteins (§9) bis ohne Befunde
- [ ] Wechselwirkungen prüfen:
  - Übersicht während eines Fenster-Ziehens
  - Randabstand nach der Rückkehr aus der Übersicht
  - Migration zusammen mit Profilen
- [ ] Sichtprüfung per KI-Debug-Schnappschuss (M16), danach Nutzer
- [ ] Spike-Ergebnis D1 in M18 übernehmen (bestanden → Spur A in M18, sonst streichen)
- [ ] Release-Notes 1.1, Status hier und in der Roadmap
- [ ] Merge nach `develop` nur mit Freigabe des Nutzers

## Akzeptanzkriterien
- [ ] Akzeptanzkriterien aller drei Feature-Pläne erfüllt
- [ ] Bestehende Installation: einmal auf Randabstand umgestellt, Hinweis erscheint genau einmal
- [ ] Kein Feature ändert gespeicherte Zoomstufen ungewollt (vorübergehende Stufe wird nie gespeichert)
- [ ] Render-Loop-Benchmarks weiter `0 B`, Bildrate = Monitor-Hz
- [ ] Alle Overlays weiter aus Aufnahmen ausgenommen (Freigabetest Teams/Browser)

## Offene Fragen / Risiken
- Apps mit eigener Ziehlogik (Browser-Tabs) melden kein Verschieben → App-Matrix in G1.
- Migrationsmerker in den Einstellungen nötig (heute keine Versionsnummer in `settings.json`).
