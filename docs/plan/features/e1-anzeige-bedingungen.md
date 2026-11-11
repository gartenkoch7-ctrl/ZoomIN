# E1 · Anzeige-Bedingungen für Markierungen
**Meilenstein:** [M18](../m18-lesbarkeit.md) · **Spur:** C (nach E2) · **Branch:** `feat/m18-e1-anzeige-bedingungen` (von `feat/m18-lesbarkeit`, nach dem Merge von E2) · **Größe:** S · **Status:** geplant

**Ziel:** Für Fadenkreuz, Caret-Markierung und Fokusrahmen wählt man, wann sie erscheinen. Dann
helfen sie, wenn man sie braucht, und stören nicht beim Arbeiten.

**Demo:** Fadenkreuz auf „nur bei gehaltener Taste“ stellen. Rechte Umschalttaste halten: das
Fadenkreuz erscheint, loslassen: es verschwindet. Caret-Markierung auf „nur in Ruhe“: Beim Tippen
ist sie aus, eine Sekunde nach dem letzten Tastendruck erscheint sie.

**Voraussetzungen:** E2 in `feat/m18-lesbarkeit` gemergt (gleiche Dateien). M5/UI-Neugestaltung
(Fadenkreuz, Eingabe-Hook).

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (E1), Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Markierungen | **Fadenkreuz**, **Caret-Markierung**, **Fokusrahmen** – jede einzeln einstellbar |
| Bedingungen | immer · nur in Ruhe · nur in Bewegung · nur bei gehaltener Taste |
| „Nur in Ruhe“ | keine Mausbewegung und kein Tippen seit 1 s (*selbst entschieden*) |
| Haltetaste | wählbar (Strg, Umschalt oder Alt, jeweils links oder rechts), Werk **Umschalt rechts** |
| Erkennung | über den bestehenden LL-Tastatur-Hook, nur lesen, nie verschlucken |
| Werk | alle drei „immer“ – Verhalten wie heute (*selbst entschieden*) |
| Geltung | Bedingungen gehören zum Profil, die Haltetaste gilt für alle Profile |

## Umsetzung
**Core (`Core/Pointer/` bzw. `Core/Tracking/`)**
- `ShowCondition` (Always, Idle, Moving, HoldKey) und `MarkerVisibility` als reine
  Entscheidungslogik. Eingaben: Zeit seit der letzten Maus- bzw. Tastenaktivität, Haltetaste
  gedrückt, Bedingung; Ergebnis: sichtbar ja/nein.
- `IdleDetector` (vorhanden) liefert die Ruhezeit. `TimeProvider` statt fester Uhr (ADR-0025).
- Snapshot `MarkerVisibilityState` (unveränderlich, atomarer Tausch): Der Render-Loop liest nur
  drei Bools, ohne Allokation.

**Platform (`Platform/Input/Win32InputActivityHook`)**
- Liefert zusätzlich „Haltetaste gedrückt/losgelassen“ für die gewählte Taste (links/rechts per
  Scan-Code bzw. `VK_RSHIFT` usw.). Nur lesen, das Ereignis geht immer weiter. Keine Allokation
  pro Ereignis.

**Rendering**
- `HighlightLayer.cs` und das Fadenkreuz prüfen vor dem Zeichnen den Snapshot. Ein Wechsel blendet
  kurz über, ca. 100 ms (*selbst entschieden*), damit nichts blitzt.

**App**
- Auf den Seiten „Mauszeiger“ (Fadenkreuz) und „Fokus & Textcursor“ (Caret, Fokusrahmen): je eine
  Auswahl „Anzeigen“. Auf der Seite „Tastenkürzel“: Haltetaste.

## Aufgaben
- [ ] Core: `ShowCondition`, `MarkerVisibility`, Snapshot, Mapping
- [ ] Tests (Core ≥ 90 %): alle vier Bedingungen × drei Markierungen, Grenze bei genau 1 s,
      Haltetaste links/rechts unterschieden, Tippen setzt die Ruhe zurück
- [ ] Benchmark Snapshot-Lesen und Entscheidung `0 B`
- [ ] Platform: Haltetaste im LL-Hook, Tests mit Fake-Hook (ADR-0025)
- [ ] Rendering: Sichtbarkeit und Überblendung in `HighlightLayer` und beim Fadenkreuz;
      WARP-Test „unsichtbar“ = keine Pixel
- [ ] UI: drei Auswahlen und die Haltetaste, Texte DE/EN, Tastatur und Bildschirmleser
- [ ] Logs App **1650–1659** (Bedingung geändert), Platform **2950–2959** (Haltetaste nicht
      erkannt/Hook neu gesetzt), Rendering **3250–3259** (nur falls nötig, keine Logs pro Frame)
- [ ] Messwert `zoomin.highlight.hold_key_presses` (Zähler, ohne Inhalte)
- [ ] Katalog `telemetrie.md`, KB `eingabe-und-tracking.md` (Anzeige-Bedingungen, Haltetaste)

## Parallelisierung
- **Exklusiv (Spur C):** `HighlightLayer.cs`, Fadenkreuz-Zeichnung, `MarkerVisibility`.
- **Hotspots, nur ergänzen:** `Win32InputActivityHook.cs` (auch Spur A6 in M19 – dann dort auf
  diesem Stand aufbauen), `AppSettings.cs`, `ProfileFields.cs`, `TrackingMapping.cs`, `Texts*.cs`,
  Seiten „Mauszeiger“, „Fokus & Textcursor“, „Tastenkürzel“, `telemetrie.md`.
- **Wartet auf:** E2 (gleiche Spur). Der Core-Teil (`MarkerVisibility`) darf parallel zu E2 entstehen.
- **Merge-Reihenfolge:** D1 → C → E2 → **E1**.

## Akzeptanzkriterien
- [ ] Jede der drei Markierungen folgt ihrer Bedingung in Vollbild, Lupe und Angedockt
- [ ] Haltetaste wirkt, ohne dass Programme die Taste verlieren (Umschalt+Buchstabe tippt weiter
      Großbuchstaben)
- [ ] Werkseinstellung verhält sich wie heute
- [ ] Keine Allokation im Render-Loop, keine Logs pro Frame
- [ ] Unsichtbar bei Bildschirmfreigabe

## Offene Fragen / Risiken
- Umschalt rechts wird beim Tippen von Großbuchstaben mitgedrückt. Dann blitzt die Markierung beim
  Schreiben kurz auf. Abhilfe ist eine Mindesthaltezeit (z. B. 250 ms); im Test mit Nutzern
  klären.
- Der LL-Hook läuft auch auf sicheren Bildschirmen (M10) nur eingeschränkt; dort gilt „immer“.
