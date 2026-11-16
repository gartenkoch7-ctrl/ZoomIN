# Vorlage – Detailplan

Regeln: [AGENTS.md §13](../../AGENTS.md). Diesen Abschnitt in die Feature-Datei kopieren
(Überschrift „Detailplan“) oder bei großen Features als `features/<id>-detail.md` anlegen.

## Detailplan

**Erstellt von:** Opus (Stufe 3) · **Datum:** JJJJ-MM-TT · **Offene Designfragen:** keine
(sonst erst klären, dann planen)

### Einschätzung

| Aufgabenpunkt | Umfang | Risiko | Neuheit | Klarheit | Prüfbarkeit | Stufe |
|---|---|---|---|---|---|---|
| Core: `XyzGeometry` | 2 Dateien, ~150 Zeilen | gering | Muster vorhanden | entschieden | Unit-Tests | 2 |
| Rendering: Shader-Modus | 2 Dateien, ~80 Zeilen | Hot Path | neu | entschieden | WARP + Benchmark | 3 |
| Texte, Katalog, KB | 4 Dateien, ~40 Zeilen | keins | Muster | entschieden | Build | 1 |

### Paket P1 – <Name> · Modell: **Sonnet** · Effort: `medium`

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/ZoomIN.Core/Tracking/Xyz.cs` | neu | `public sealed record Xyz(int A, int B)`; Methode `Compute(PhysicalRect r, double zoom)` – wie `CaretMarkerGeometry` | – | 40 |
| `tests/ZoomIN.Core.Tests/Tracking/XyzTests.cs` | neu | Fälle: 1×, 4×, 16×, Monitorrand | selbst | 80 |
| `src/ZoomIN.App/Settings/TrackingMapping.cs` | ändern | `Map(...)`: Feld `Xyz` ergänzen (nur ergänzend, Hotspot) | Mapping-Test | 10 |

- **Reihenfolge:** Core → Tests → Mapping
- **Telemetrie:** EventIds aus dem Bereich der Spur (z. B. App NNNN–NNNN), Activity `zoomin.xyz.compute` – Eintrag im Katalog in P3
- **Akzeptanz:** `dotnet test --solution ZoomIN.slnx` grün, neue Tests decken alle Fälle
- **Eskalation:** zwei rote Läufe oder eine nicht geplante Entscheidung → stoppen und melden

### Paket P2 – <Name> · Modell: **Opus** · Effort: `high`
…

### Paket P3 – Texte, Katalog, KB · Modell: **Haiku** · Effort: `low`
…

### Rückblick (nach Abschluss)
| Paket | geplante Stufe | passend? | Anmerkung |
|---|---|---|---|
