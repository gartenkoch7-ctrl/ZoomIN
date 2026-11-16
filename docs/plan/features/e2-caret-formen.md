# E2 · Caret-Formen
**Meilenstein:** [M18](../m18-lesbarkeit.md) · **Spur:** C (zuerst E2, dann E1) · **Branch:** `feat/m18-e2-caret-formen` (von `feat/m18-lesbarkeit`) · **Größe:** S · **Status:** geplant

**Ziel:** Der Textcursor lässt sich in Form, Farbe und Dicke einstellen und wächst mit dem Zoom,
damit man ihn beim Tippen sofort findet.

**Demo:** In Word bei 6× tippen. Gelbe Keile über und unter dem Cursor laufen mit, gut sichtbar
und dicker als der Windows-Cursor. Auf Kreis umstellen: Die Änderung wirkt sofort, auch in der
Lupe.

**Voraussetzungen:** M6 (Caret-Kette, Caret-Balken). Darf schon während M17 beginnen.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (E2), Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Form | **Balken** (heute) · **Keile** über und unter dem Cursor · **Rahmen** um das Zeichen · **Kreis** |
| Farbe | Voreinstellungen wie auf der Mauszeiger-Seite (Gelb, Grün, Cyan, Magenta, Weiß, Schwarz) und freier Farbwähler |
| Dicke | dünn · mittel · dick, wächst mit dem Zoom |
| Fokusrahmen | bleibt unverändert (nicht gewählt) |
| Wort/Zeile markieren | nicht gewählt |
| Werk | Balken, Magenta, mittel – sieht aus wie heute (*selbst entschieden*) |
| Geltung | gehört zum Profil |

## Umsetzung
**Core**
- `CaretMarkerStyle(Shape, Color, Thickness)` als Record in `Core/Tracking/`.
- Reine Geometrie in `CaretMarkerGeometry`: Aus dem Caret-Rechteck, der Zoomstufe und der Dicke
  entstehen die Teil-Rechtecke bzw. Parameter für Keile, Rahmen und Kreis. Testbar, keine
  Allokation.
- `TrackingOptions.CaretMarker` (bool) bleibt als an/aus; die neue Form kommt dazu.
- Bei geschätztem Caret (`CaretEstimated`) wird weiterhin nichts gezeichnet.

**Rendering**
- `HighlightLayer.cs`: Die fest eingebauten Konstanten (Zeilen 12–16) werden für den Caret durch
  den Stil aus dem Snapshot ersetzt. Der Fokusrahmen behält seine Konstanten.
- `Cursor.hlsl`: Neuer Pixel-Shader bzw. neuer Modus von `PSRect` für Keile (Dreieck per
  Abstandsfunktion) und Kreis (Ring); Balken und Rahmen nutzen das bestehende `PSRect`.
- Größe in Bildschirmpixeln = Grunddicke × Zoomfaktor, mit Obergrenze wie beim Zeiger.

**App**
- Seite „Fokus & Textcursor“: Karte „Textcursor-Markierung“ mit Form, Farbe, Dicke und kleiner
  Vorschau.

## Aufgaben
- [x] Core: `CaretMarkerStyle`, `CaretMarkerGeometry`, Mapping in `TrackingMapping.cs`
- [x] Tests (Core ≥ 90 %): jede Form bei 1×, 4×, 16×; Rand des Monitors; geschätzter Caret zeichnet
      nichts
- [x] Benchmark `CaretMarkerGeometry` und Snapshot-Pfad `0 B`
- [x] Rendering: `HighlightLayer` liest den Stil, Shader-Modi für Keile und Kreis
- [x] WARP-Tests: Pixel der Keile über und unter dem Caret, Ring um den Mittelpunkt, Farbe korrekt
- [x] UI-Karte, Texte DE/EN, vollständig per Tastatur und Bildschirmleser bedienbar
      (Vorschau mit Textbeschreibung)
- [x] Logs App **1640–1649** (Stil geändert: Form, Dicke), Rendering **3240–3249** (Shader-Modus
      fehlt/Fallback auf Balken). *Umsetzung:* 3240–3249 bleibt frei – Shader sind beim Build
      kompiliert und geprüft, ein Rückfall wäre toter Code und würde Geräteverlust verdecken.
- [x] Messwert `zoomin.highlight.caret_shape` als Tag des bestehenden Zeichen-Messwerts, beim Start
      angelegt (nicht pro Frame neu). *Umsetzung:* Einen Zeichen-Messwert gab es noch nicht; neu
      ist der Zähler `zoomin.highlight.caret_frames` mit diesem Tag.
- [x] Katalog `telemetrie.md`, KB `eingabe-und-tracking.md` (Caret-Formen)

## Parallelisierung
- **Exklusiv (Spur C):** `HighlightLayer.cs`, `Cursor.hlsl` (nur die Caret-Modi),
  `CaretMarkerGeometry`.
- **Hotspots, nur ergänzen:** `AppSettings.cs`, `ProfileFields.cs`, `TrackingMapping.cs`,
  `Texts*.cs`, Seite „Fokus & Textcursor“, `telemetrie.md`.
- **Wartet auf:** nichts (darf während M17 beginnen). E1 kommt in derselben Spur danach, weil beide
  `HighlightLayer` und `Cursor.hlsl` anfassen.
- **Merge-Reihenfolge:** D1 → C → **E2** → E1.

## Akzeptanzkriterien
- [ ] Alle vier Formen in Vollbild, Lupe und Angedockt sichtbar und richtig am Caret
- [ ] Farbe und Dicke wirken sofort; Werkseinstellung sieht aus wie heute
- [ ] Caret-Markierung wächst mit dem Zoom und bleibt am Monitorrand vollständig
- [ ] Keine Allokation im Render-Loop, Bildrate unverändert
- [ ] Unsichtbar bei Bildschirmfreigabe

## Offene Fragen / Risiken
- Rahmen um das Zeichen braucht die Breite des Zeichens. Die Caret-Kette liefert sie über
  `ExpandToEnclosingUnit(Character)` nicht überall; fehlt sie, gilt eine geschätzte Breite
  (Höhe × 0,5).
- Keile über und unter dem Cursor können am oberen und unteren Monitorrand abgeschnitten werden;
  dann werden sie in den Monitor geklemmt.
