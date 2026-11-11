# B2 · Übersicht auf Knopfdruck
**Meilenstein:** [M17](../m17-ruhige-ansicht.md) · **Spur:** B · **Branch:** `feat/m17-b2-uebersicht` · **Größe:** S–M · **Status:** geplant

**Ziel:** Bei hoher Stufe schnell sehen, wo man ist. Ein Kürzel schaltet den Monitor vorübergehend
auf 1×. Ein Rahmen zeigt den bisherigen Ausschnitt und folgt der Maus. Danach zoomt man dort hinein,
wo der Rahmen steht.
**Demo:** Vollbild 6× → `Strg+Alt+O` → Bild gleitet auf 1×, Rahmen um die Maus → Rahmen zum
Startmenü schieben → `Strg+Alt+O` → Zoom gleitet dort auf 6×. Dasselbe mit gehaltenem Kürzel
(loslassen = hineinzoomen) und mit Esc (zurück zur alten Stelle).
**Voraussetzungen:** Schritt 0 „vorübergehende Zoomstufe“ in `feat/m17-ruhige-ansicht`; A1
gemergt (Anker für das Hineinzoomen).

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (B2, enthält B6), Bewertung:
[feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Kürzel | `Strg+Alt+O`, anpassbar wie alle Kürzel |
| Antippen | Übersicht an; Maus bewegt den Rahmen; erneut antippen = am Rahmen hineinzoomen |
| Esc | zurück zur alten Stelle und Stufe; Esc wird nur verschluckt, solange die Übersicht aktiv ist |
| Halten | Übersicht nur solange gehalten; Loslassen = am Rahmen hineinzoomen |
| Grenze tippen/halten | kürzer als 300 ms = Antippen *(selbst entschieden)* |
| Rahmen | Größe = bisheriger Ausschnitt bei der alten Stufe, Farbe = Lupenrahmen-Farbe *(selbst entschieden)* |
| Modi | Vollbild und Lupe; Angedockt: Kürzel wirkt nicht *(selbst entschieden)* |
| Filter | bleiben in der Übersicht aktiv |
| Stufe | wird nicht gespeichert (vorübergehende Stufe aus Schritt 0) |

## Umsetzung
- **Core**
  - `OverviewModel` (Zustand Aus/Angetippt/Gehalten, alte Stufe und alter Anker pro Monitor) als
    reine Logik.
  - Nutzt die vorübergehende Stufe aus Schritt 0 (`ZoomModel`) für 1× und das Zurück.
  - Rahmenrechteck aus `ModeGeometry` bei alter Stufe um die Mausposition, im Monitor gehalten.
  - Hineinzoomen setzt den Anker so, dass die Rahmenmitte nach dem Gleiten in der Bildmitte steht.
  - Die Tipp-/Halten-Grenze ist eine reine Funktion über Zeitstempel (`TimeProvider`).
- **Platform**
  - `Win32HotkeyService` liefert nur das Drücken (WM_HOTKEY), kein Loslassen. Das Loslassen von
    `O` bzw. Strg/Alt erkennt der vorhandene `Win32InputActivityHook` (WH_KEYBOARD_LL). Er
    bekommt ein Signal „Kürzeltaste losgelassen“.
  - Esc verschluckt der Hook nur, solange ein Flag „Übersicht aktiv“ gesetzt ist.
- **Rendering:** `HighlightLayer`/`PSRect` zeichnet den Übersichtsrahmen. Die Werte kommen aus dem
  Snapshot, ohne Allokation pro Frame.
- **App**
  - `HotkeyGesture`/`HotkeyBindings`: neue Aktion `Overview` (Werk `Strg+Alt+O`).
  - Seite „Tastenkürzel“: Eintrag mit Erklärung.
  - Übersicht-Startseite: kurzer Hinweis auf das Kürzel.

## Aufgaben
### Core
- [ ] `OverviewModel` + Tests: Antippen/erneut, Halten/Loslassen, Esc, Monitorwechsel während
      Übersicht, Lupe, Zoom vorher aus (Kürzel ohne Wirkung), Grenze 300 ms
- [ ] Rahmen- und Ankerrechnung mit Tests (Rahmenmitte landet in Bildmitte, Monitorrand)
- [ ] Abdeckung ≥ 90 %
### Platform (EventIds 2920–2939)
- [ ] Loslassen-Signal im LL-Hook, Esc nur bei aktivem Flag verschlucken; Log 2920 Esc verschluckt
      (nur Zähler, keine Tasten des Nutzers)
### Rendering (EventIds 3100–3119)
- [ ] Rahmen im Highlight-Durchgang, WARP-Test (Rahmen an richtiger Stelle, Rest transparent)
- [ ] Benchmark Frame mit Rahmen `0 B`
### App (EventIds 1520–1539)
- [ ] Kürzel-Bindung, Seite „Tastenkürzel“, Texte DE/EN, Bildschirmleser-Ansage „Übersicht an/aus“
- [ ] Ablauf `zoomin.view.overview` (Start bis Hineinzoomen/Abbruch; Tags Art = tippen/halten,
      Ende = hineinzoomen/esc, Stufe vorher)
- [ ] Katalog `telemetrie.md`, KB `eingabe-und-tracking.md` (Abschnitt Übersicht)
- [ ] KI-Debug: Übersicht als auslösbarer Kurzbefehl im Werkzeugkatalog (für Sichtprüfungen)

## Parallelisierung
- **Exklusiv:** `OverviewModel`, Rahmen-Teil im `HighlightLayer`, Loslassen-Signal im
  `Win32InputActivityHook`.
- **Hotspots, nur ergänzend:** `HotkeyGesture.cs`, `HotkeyBindings.cs`, `AppSettings.cs` (Kürzel),
  `Texts*.cs`, `telemetrie.md`, `eingabe-und-tracking.md`, KI-Debug-Werkzeugkatalog.
- **Wartet auf:** Schritt 0, A1 (Merge vor B2).
- **Parallel zu:** G1 (beide nutzen Schritt 0 nur lesend/aufrufend, ändern `ZoomModel.cs` nicht).
- **Merge:** nach A1. Die Reihenfolge zu G1 ist egal.

## Akzeptanzkriterien
- [ ] Antippen, Halten und Esc verhalten sich wie oben (Sichtprüfung per KI-Debug-Schnappschuss)
- [ ] Nach dem Hineinzoomen ist die gespeicherte Stufe unverändert
- [ ] Esc außerhalb der Übersicht erreicht weiter die Anwendung
- [ ] Rahmen ist nicht in Bildschirmaufnahmen sichtbar
- [ ] Benchmark `0 B`, Bildrate unverändert

## Offene Fragen / Risiken
- Halten von `Strg+Alt+O` ist für Nutzer mit motorischen Einschränkungen anstrengend. Antippen
  bleibt deshalb der Hauptweg.
- LL-Hook und UIAccess auf sicheren Bildschirmen: Die Übersicht ist dort zunächst nicht vorgesehen
  (M10 prüfen).
