# C1–C3 · Eigene Farbschemata
**Meilenstein:** [M18](../m18-lesbarkeit.md) · **Spur:** B · **Branch:** `feat/m18-c-farbschemata` (von `feat/m18-lesbarkeit`) · **Größe:** M · **Status:** geplant

**Ziel:** Nutzer legen bis zu 4 eigene, benannte Farbschemata an. Ein Schema besteht aus zwei
freien Farben, einem Farbton oder einer Korrektur für eine Farbsehschwäche, dazu bis zu
3 Farbersetzungen. Jedes Schema verhält sich wie ein eingebauter Filter.

**Demo:** Ein Schema „Grün auf Dunkelblau“ anlegen. Es erscheint sofort in der Filterliste, beim
Durchschalten mit `Strg+Alt+Umschalt+C` und in den Regeln für einzelne Fenster. Ein zweites Schema
„Bernstein“ färbt das ganze Bild ein. Ein drittes korrigiert eine Rot-Grün-Schwäche zu 70 % und
ersetzt Rot durch Gelb.

**Voraussetzungen:** M5 (Farbmatrizen), M12 (Filter pro Fenster, Palette), M13 (Smart Invert).
Der Core-Teil darf schon während M17 beginnen.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (C1–C3), Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Arten | **Zwei Farben** (hell → Farbe 1, dunkel → Farbe 2) · **Einfärben** (ein Farbton, die Helligkeit bleibt) · **Farbsehschwäche-Korrektur** für Protan, Deutan und Tritan, Stärke 0–100 % · **Farbe ersetzen** |
| Farbsehschwäche | nur Korrektur, keine Simulation; Matrizen selbst aus der Fachliteratur hergeleitet |
| Farbe ersetzen | bis zu 3 Ersetzungen je Schema, jede mit Toleranzregler |
| Anzahl | bis zu 4 benannte eigene Schemata |
| Sichtbarkeit | in allen Filterlisten, in den Regeln für einzelne Fenster und beim Durchschalten mit `Strg+Alt+Umschalt+C` |
| Geltung | Die Definitionen der Schemata gelten für alle Profile, die Auswahl gehört zum Profil (*selbst entschieden*) |
| Werk | keine eigenen Schemata angelegt; die eingebauten Filter bleiben unverändert |

## Umsetzung
**Core (`Core/Filters/`)**
- Der Filter wird **datengetrieben**. `ColorFilter` (Enum, 8 Werte) wird zu einer `FilterId`: die
  8 eingebauten Filter plus 4 Plätze für eigene Schemata. JSON bleibt rückwärtskompatibel, die
  alten Enum-Namen werden weiter gelesen.
- `CustomScheme(Name, Kind, Color1, Color2, Hue, Deficiency, Strength, Replacements[≤3])` und
  `ColorReplacement(From, To, Tolerance)`.
- `ColorFilters.For()` liefert für eigene Schemata die Matrix:
  - Zwei Farben: Leuchtdichte als affiner Verlauf `c2 + L·(c1 − c2)`
  - Einfärben: Leuchtdichte × Farbton
  - Farbsehschwäche: Korrekturmatrix, mit der Stärke zur Einheitsmatrix gemischt
- Die Ersetzungen sind **keine** Matrix. Sie gehen als eigener Parameterblock an den Shader.
- `DisplayOrder` (fest eingebaut) wird zu einer Liste aus den eingebauten Filtern und den
  angelegten Schemata.
- `WindowPalette`: Größe 2 + 12 = **14** statt `Enum.GetValues`. Die Regeln für einzelne Fenster
  speichern die `FilterId`.
- `EffectsModel.Next()` schaltet über alle Einträge, eigene Schemata eingeschlossen.

**Rendering (`Shaders/Magnify.hlsl`, `DesktopLayer`)**
- `PALETTE_SIZE` steigt von 10 auf 14, der Constant Buffer wächst entsprechend.
- Neuer Schritt **nach dem Sampling und vor `Apply(...)`**: bis zu 3 Ersetzungen, gewichtet über
  den Farbabstand im Lab- oder YCbCr-Raum mit weichem Übergang über die Toleranz. Er wird nur
  ausgeführt, wenn das aktive Schema Ersetzungen hat; das steuert ein Flag im Constant Buffer.
- Er verträgt sich mit Smart Invert (`SmartWeight.hlsli`): Erst wird ersetzt, dann gewichtet.

**App**
- Farbfilter-Seite: Karte „Eigene Schemata“ führt auf eine Detailseite mit Liste, Neu, Bearbeiten
  (Art, Farben, Stärke, Ersetzungen), Umbenennen, Löschen und Live-Vorschau.
- Löscht man ein Schema, das in einer Fensterregel oder einem Profil steckt, fallen diese auf
  „Invertiert“ zurück, mit Hinweis (*selbst entschieden*).
- Farbwähler wie auf der Mauszeiger-Seite.

## Aufgaben
### 1 · Core
- [ ] `FilterId` statt `ColorFilter`, JSON-Lesen der alten Namen, Migration der Regeln für
      einzelne Fenster
- [ ] `CustomScheme`, `ColorReplacement`, Matrizen für zwei Farben, Einfärben und die drei
      Farbsehschwächen (Herleitung mit Quelle in `docs/kb/`)
- [ ] `WindowPalette` mit 14 Plätzen, `DisplayOrder` dynamisch, `EffectsModel.Next` über alle
- [ ] Tests (Core ≥ 90 %): jede Art mit Weiß/Schwarz/Rot, Stärke 0 % = Einheitsmatrix,
      Migration alter Settings, Durchschalten, Palette-Index stabil
- [ ] Benchmark: Matrix- und Palettenaufbau `0 B` im Snapshot-Pfad

### 2 · Rendering
- [ ] `PALETTE_SIZE` 14, Constant Buffer erweitert
- [ ] Ersetzungs-Schritt (≤ 3) mit Toleranz und Flag
- [ ] WARP-Tests: Ersetzung trifft Zielfarbe, Toleranz 0 lässt Nachbarfarben unverändert,
      Zusammenspiel mit Smart Invert und Fensterregeln
- [ ] GPU-Zeit messen (RTX 3060 Ti, 1080p, ×10 hochgerechnet): Gesamtframe bleibt ≤ 2 ms Onboard

### 3 · App und Telemetrie
- [ ] Detailseite „Eigene Schemata“ inkl. Vorschau, Texte DE/EN, vollständig per Tastatur und
      Bildschirmleser bedienbar
- [ ] Logs App **1620–1639**: Schema angelegt, geändert, gelöscht, Rückfall einer Regel (nur Art
      und Platz, kein Name)
- [ ] Logs Rendering **3220–3239**: Palette neu gebaut, Ersetzungs-Schritt aktiv
- [ ] Ablauf `zoomin.filters.scheme_edit`; Messwert `zoomin.filters.custom_schemes` (Anzahl)
- [ ] Katalog `docs/kb/telemetrie.md`, KB `rendering-pipeline.md` (neuer Schritt, Palette 14)

## Parallelisierung
- **Exklusiv:** `Core/Filters/*`, `Core/WindowFilters/WindowPalette.cs`, `Magnify.hlsl`,
  Detailseite „Eigene Schemata“.
- **Hotspots, nur ergänzen:** `AppSettings.cs`, `ProfileFields.cs`, `EffectsMapping.cs`,
  `Texts*.cs`, Farbfilter-Seite, `telemetrie.md`.
- **Wartet auf:** nichts für den Core-Teil (kann während M17 beginnen). Der Shader-Teil wartet auf
  den Merge von D1 in `feat/m18-lesbarkeit`, weil D1 die Pass-Struktur in `DesktopLayer` ändert.
- **Merge-Reihenfolge:** D1 → **C** → E2/E1.

## Akzeptanzkriterien
- [ ] 4 eigene Schemata anlegbar; ein fünftes wird mit Hinweis abgelehnt
- [ ] Eigene Schemata erscheinen in der Filterliste, beim Durchschalten mit
      `Strg+Alt+Umschalt+C` und in den Regeln für einzelne Fenster, auch nach einem Neustart
- [ ] Alte `settings.json` (Enum-Namen) wird ohne Verlust gelesen
- [ ] „Rot → Gelb“ ersetzt Rot sichtbar, andere Farben bleiben (Toleranz klein)
- [ ] Bildrate gleich Monitor-Hz, GPU-Budget eingehalten, keine Allokation im Render-Loop
- [ ] Bei Bildschirmfreigabe unsichtbar wie alle Filter

## Offene Fragen / Risiken
- Umbau von Enum zu `FilterId` berührt M12/M13-Code (Regeln, Palette, Smart Invert); die
  Regressionstests von M12/M13 müssen grün bleiben.
- Ersetzung kostet pro Pixel bis zu 3 Farbabstände; bei zu hohen Kosten Grenze auf 1–2 senken
  (Rücksprache).
- Korrekturmatrizen für Farbsehschwächen sind Näherungen; mit Betroffenen testen.
