# M19 – Mehrere Ansichten

**Ziel:** Man sieht mehrere Stellen gleichzeitig, ohne den Überblick oder die Klicksicherheit zu
verlieren:
- ein Monitor bleibt eingefroren, während man auf dem anderen arbeitet
- kleine vergrößerte Inseln (Uhr, Chat) liegen über dem Zoom
- ein Kürzel wechselt den Monitor, und der Zeiger rutscht nicht mehr versehentlich hinüber

**Demo:**
- Drei Monitore (1 links, 3 Mitte, 2 rechts). Anleitung auf 2 vergrößert, `Strg+Alt+H` → 2 bleibt
  stehen, der Zeiger springt auf 3 und kann nicht zurück auf 2.
- `Strg+Alt+N` → Zeiger auf 1, erneut → zurück auf 3.
- `Strg+Alt+Umschalt+I` → Insel um die Uhr aufziehen, Esc → die Uhr bleibt vergrößert sichtbar.

**Voraussetzungen:** M18 in `develop` gemergt. [ADR-0028](../adr/0028-zeiger-versetzen-nur-auf-befehl.md).
Release als **1.3**.
**Herkunft:** [feature-priorisierung.md](../kb/feature-priorisierung.md) (Bündel „Mehrere
Ansichten“), Grilling 2026-11-11.

## Features
| ID | Feature | Plan | Größe |
|---|---|---|---|
| G2 | Monitor einfrieren + Monitorwechsel | [g2-einfrieren-monitorwechsel.md](features/g2-einfrieren-monitorwechsel.md) | M |
| B1 | Eingefrorene Inseln | [b1-inseln.md](features/b1-inseln.md) | M |
| A6 | Maus-Puffer und Achsensperre | [a6-maus-puffer-achsensperre.md](features/a6-maus-puffer-achsensperre.md) | S |

## Gemeinsame Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Zeiger versetzen | nur auf ausdrücklichen Befehl (Einfrieren, Monitorwechsel), nie automatisch → ADR-0028 |
| Eingefrorener Monitor | Zeiger kommt nicht hinein, bis aufgehoben; damit keine Fehlklicks |
| Inhalt | live ab Werk, Standbild wählbar (Monitor und Inseln) |
| Neustart | Inseln bleiben (Profil), eingefrorene Monitore nicht |
| Kürzel | `Strg+Alt+H` einfrieren/alle aufheben · `Strg+Alt+N` nächster Monitor · `Strg+Alt+I` Inseln ein/aus · `Strg+Alt+Umschalt+I` Inseln bearbeiten |

## Parallelisierung
Integrationsbranch **`feat/m19-mehrere-ansichten`** (von `develop` nach M18). Regeln: [AGENTS.md §11](../../AGENTS.md).

```
Schritt 0: Zeigerzaun + Standbild ──┬─► Spur A: G2 Einfrieren + Monitorwechsel ─┐
                                    ├─► Spur B: B1 Inseln ──────────────────────┼─► Integration ─► Review ─► develop
                                    └─► Spur C: A6 Puffer + Achsensperre ───────┘
```

| Spur | Inhalt | Branch | Start | Wartet auf | EventIds |
|---|---|---|---|---|---|
| 0 | Bausteine „Zeigerzaun“ (`IPointerFence`, `FenceGeometry`) und „Standbild“ (Frame-Kopie) | `feat/m19-mehrere-ansichten` | sofort | ADR-0028 | App 1700–1709, Platform 2940–2949, Rendering 3300–3309 |
| A | G2 | `feat/m19-g2-einfrieren` | nach Schritt 0 | Schritt 0 | App 1720–1739, Rendering 3310–3319 |
| B | B1 | `feat/m19-b1-inseln` | nach Schritt 0 | Schritt 0 | App 1740–1759, Rendering 3320–3339 |
| C | A6 | `feat/m19-a6-maus-puffer` | nach Schritt 0 | Schritt 0 (Zaun) | App 1760–1779, Platform 2960–2979 |

**Merge-Reihenfolge:** C → A → B.
- A6 ist klein und prüft den Zaun zuerst im Alltag.
- B1 kommt zuletzt, weil es den Render-Pfad am stärksten ändert.

**Exklusiv:**
- `IPointerFence` / Zaun-Implementierung und Standbild-Kopie: Schritt 0. Danach ändert sie nur noch
  die Integration.
- `ZoomModel.cs` (eingefrorener Anker): Spur A
- `DesktopLayer.cs`, `Magnify.hlsl` (Inseln): Spur B
- Maus-Hook-Erweiterung für Puffer und Achse: Spur C, über Schnittstellen des Zauns

**Hotspots** (nur ergänzend):
- `AppSettings.cs`, `ProfileFields.cs`
- `HotkeyGesture.cs`, `HotkeyBindings.cs`
- `Texts*.cs`
- `docs/kb/telemetrie.md`, `docs/kb/eingabe-und-tracking.md`

**Nutzen:** etwa 3 Wochen statt 5 hintereinander.

## Aufgaben (Integration)
- [ ] ADR-0028 liegt vor; Integrationsbranch anlegen, Schritt 0 mit Tests, Review (§9), Commit
- [ ] Spuren A, B, C gleichzeitig starten (Worktree je Spur)
- [ ] Mergen C → A → B, nach jedem Merge Build + Tests
- [ ] Gesamt-Review (§9) bis ohne Befunde
- [ ] Wechselwirkungen prüfen:
  - Puffer am Rand zu einem eingefrorenen Monitor
  - Inseln auf einem eingefrorenen Monitor
  - Übersicht (M17) und Fenster-Verschieben (M17) bei eingefrorenem Monitor
  - Sperren mit Win+L, UAC und sichere Bildschirme (Zaun und Einfrieren aufheben)
- [ ] Test mit drei Monitoren (virtuelle Monitore, M3-Treiber) inkl. Lage 1 · 3 · 2
- [ ] Sichtprüfung per KI-Debug-Schnappschuss, danach Nutzer
- [ ] Release-Notes 1.3, Status hier und in der Roadmap; Merge nach `develop` nur mit Freigabe

## Akzeptanzkriterien
- [ ] Akzeptanzkriterien aller Feature-Pläne erfüllt
- [ ] Nach Win+L, Strg+Alt+Entf, UAC oder Anzeigewechsel ist der Zeiger nie dauerhaft eingesperrt
- [ ] Beenden oder Absturz von ZoomIN gibt den Zeiger sofort frei (kein `ClipCursor` bleibt stehen)
- [ ] Render-Loop `0 B`, alle Overlays und Inseln aus Aufnahmen ausgenommen

## Offene Fragen / Risiken
- Windows setzt `ClipCursor` bei manchen Ereignissen zurück. Der Zaun muss es zuverlässig wieder
  setzen, darf aber nach einem Absturz nichts sperren.
- Ein Maus-Hook, der Bewegungen verschluckt, ist heikel bei Spielen und Fernsteuerung. Dafür
  braucht es eine Ausnahmeliste.
- Mit nur einem echten Monitor sind drei Monitore nur virtuell testbar ([testumgebung.md](../kb/testumgebung.md)).
