# G2 · Monitor einfrieren und Monitor wechseln
**Meilenstein:** [M19](../m19-mehrere-ansichten.md) · **Spur:** A · **Branch:** `feat/m19-g2-einfrieren` ·
**Größe:** M · **Status:** geplant

**Ziel:** Ein Monitor behält seinen vergrößerten Ausschnitt, während man auf einem anderen weiterarbeitet.
Mit einem Kürzel wechselt der Zeiger zum nächsten Monitor.

**Demo:** Drei Monitore, Lage 1 (links) · 3 (Mitte) · 2 (rechts). Auf Monitor 2 läuft eine Anleitung
bei 4×. `Strg+Alt+H` friert Monitor 2 ein, und der Zeiger springt sofort auf 3. Die Anleitung bleibt
vergrößert stehen, ein Video darin läuft weiter. Die Maus kommt nicht auf Monitor 2. `Strg+Alt+N`
setzt den Zeiger auf 1, ein zweites Mal wieder auf 3. Ein erneutes `Strg+Alt+H` hebt das Einfrieren
auf, und der Zeiger darf wieder auf 2.

**Voraussetzungen:** M19 Schritt 0: [ADR-0028](../../adr/0028-zeiger-versetzen-nur-auf-befehl.md), die
Bausteine „Zeigerzaun“ (`IPointerFence`) und „Standbild“.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (G2, A7, F3) ·
Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md) ·
Grundsatz: [ADR-0028](../../adr/0028-zeiger-versetzen-nur-auf-befehl.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Einfrieren | `Strg+Alt+H` friert den Monitor unter dem Zeiger ein. Ist schon einer eingefroren, hebt dasselbe Kürzel **alle** auf. |
| Zeiger beim Einfrieren | Der Zeiger springt sofort zum nächstgelegenen freien Monitor. Das ist ein ausdrücklicher Befehl im Sinne von ADR-0028. |
| Sperre | Der Zeiger kann nicht auf einen eingefrorenen Monitor, bis das Einfrieren aufgehoben ist. Damit gibt es dort keine Fehlklicks. |
| Grenzen | Der letzte freie Monitor kann nicht eingefroren werden, es kommt ein Hinweis. Bei nur einem Monitor gibt es kein Einfrieren. *(selbst entschieden)* |
| Inhalt | Ab Werk live. Standbild ist wählbar (Einstellung). |
| Neustart | Nach einem Neustart ist nichts eingefroren. |
| Anzeige | Ein eingefrorener Monitor bekommt einen deutlichen Rahmen. |
| Verfolgung | Fokus- und Caret-Verfolgung pausieren auf eingefrorenen Monitoren. |
| Monitorwechsel | `Strg+Alt+N`. Die Reihenfolge folgt der Lage der Monitore, nicht der Windows-Nummer. An den Enden kehrt sie um: 1 → 3 → 2 → 3 → 1. Aus der Mitte geht es zuerst nach rechts. Eingefrorene Monitore werden übersprungen. |
| Ziel des Zeigers | Seine letzte Position auf diesem Monitor, sonst die Mitte. |
| Folge | Im Beispiel (rechts eingefroren) steht der Zeiger schon auf 3, und das erste `Strg+Alt+N` führt auf 1. |

## Umsetzung
**Core**
- `Core/Monitors/MonitorCycle` (rein): Aus der Liste der Monitorrechtecke (Lage), dem aktuellen
  Monitor, der letzten Richtung und der Menge der eingefrorenen Monitore berechnet sie den
  nächsten Monitor. Sie liefert auch den nächstgelegenen freien Monitor für den Sprung beim Einfrieren.
  - Sortiert wird nach der Mitte der Monitore: zuerst x, bei gleicher Spalte y.
  - Die Richtung wird an den Enden umgekehrt, ohne Allokation.
- `Core/Monitors/FreezeState`: unveränderliche Menge eingefrorener Monitore samt Standbild-Wahl.
  Sie wird per `Volatile` getauscht wie die Ziele im `ZoomModel`.
- `Core/Monitors/PointerMemory`: letzte Zeigerposition je Monitor (`PhysicalPoint`).
- `ZoomModel` (Hotspot, nur hier): `Freeze(monitorId)`, `UnfreezeAll()` und `IsFrozen(monitorId)`
  lesen lock-frei.

**Rendering**
- `RenderSession`/`AnchorController`: Ist der Monitor eingefroren, wird der Anker nicht
  weitergeschoben (`anchors.Next` gibt den letzten Anker zurück). Die Zoomstufe bleibt stehen.
- Zeiger, Fokusrahmen und Caret werden auf eingefrorenen Monitoren nicht gezeichnet. Der Zeiger
  kann ohnehin nicht dorthin.
- Standbild: Ist es gewählt, nutzt der Monitor den Baustein „Standbild“ aus Schritt 0 und liest
  keinen neuen Capture-Frame mehr.
- Rahmen: Der vorhandene Rahmen im `Magnify.hlsl` (wie bei der Lupe) bekommt die Farbe
  „eingefroren“. `WDA_EXCLUDEFROMCAPTURE` bleibt unverändert.

**Platform**
- `IPointerFence` aus Schritt 0 bekommt die Rechtecke der eingefrorenen Monitore.
- Der Sprung des Zeigers geht über `IPointerMover.MoveTo(PhysicalPoint)` (`SetCursorPos`, in Schritt
  0 oder hier anzulegen). Aufgerufen wird er nur aus den beiden Kürzeln, nach ADR-0028.

**App**
- `HotkeyBindings` (Hotspot): `FreezeMonitor` mit `Strg+Alt+H` und `NextMonitor` mit `Strg+Alt+N`,
  beide anpassbar.
- `MonitorFreezeController`: verbindet Kürzel, `MonitorCycle`, `FreezeState`, Zaun und Mover.
  Er zeigt den Hinweis, wenn kein freier Monitor übrig bliebe.
- Einstellungen (global, nicht im Profil): Inhalt eingefrorener Monitore „Live“ oder „Standbild“,
  Rahmenfarbe. Die Kürzel stehen auf der Seite „Tastenkürzel“.

## Aufgaben
- [ ] Core: `MonitorCycle`, `FreezeState`, `PointerMemory` mit Tests (Abdeckung ≥ 90 %). Fälle: 1, 2
      und 3 Monitore, Hin und Zurück, Mitte zuerst rechts, eingefrorene überspringen, gestapelte
      Monitore, letzter freier Monitor.
- [ ] Core: Beispiel aus dem Grilling als Test (1 links · 3 Mitte · 2 rechts).
- [ ] `ZoomModel` um den Freeze-Zustand erweitern, Tests für lock-freies Lesen.
- [ ] Rendering: Anker und Stufe einfrieren, Zeiger und Markierungen auf eingefrorenen Monitoren
      unterdrücken, Rahmen. WARP-Test „Anker bleibt, Inhalt ändert sich“.
- [ ] Benchmark: Render-Pfad mit eingefrorenem Monitor `0 B`. `MonitorCycle` `0 B`.
- [ ] Platform: Mover (`SetCursorPos`) mit Interface, Zaun mit den Monitorrechtecken.
- [ ] App: Kürzel, Controller, Hinweis „letzter freier Monitor“, Einstellungen.
- [ ] Telemetrie (ADR-0019):
  - Abläufe `zoomin.view.freeze` (Tags: Monitor-Index, Stufe, live/Standbild) und
    `zoomin.view.monitor_switch` (von, nach, übersprungen)
  - Zähler `zoomin.view.frozen_monitors`
  - Logs App 1720–1739, Rendering 3310–3319
  - Alles im Katalog `docs/kb/telemetrie.md`
- [ ] Texte Deutsch/Englisch. Bildschirmleser sagt Einfrieren, Aufheben und Wechsel an (z. B.
      „Monitor 2 eingefroren“).
- [ ] KB: `docs/kb/eingabe-und-tracking.md` (Zeiger versetzen, Sperre, Reihenfolge) und
      `docs/kb/rendering-pipeline.md` (eingefrorener Anker).
- [ ] Sicht- und Freigabetest: Rahmen und eingefrorener Monitor bleiben in Teams/OBS unsichtbar.
- [ ] Sichere Bildschirme (M10): Einfrieren beim Wechsel aufheben, Zaun lösen.

## Parallelisierung
- **Gehört allein dieser Spur:**
  - `Core/Monitors/*`
  - `MonitorFreezeController`
  - die Freeze-Erweiterung in `ZoomModel.cs`
  - die Anker-Sperre in `AnchorController`/`RenderSession`
- **Hotspots, nur ergänzen:**
  - `AppSettings.cs`, `HotkeyGesture.cs`/`HotkeyBindings.cs`, `Texts*.cs`
  - `docs/kb/telemetrie.md`, `docs/kb/eingabe-und-tracking.md`
  - `Magnify.hlsl` nur über den vorhandenen Rahmen-Parameter, keine neuen Shader-Teile (die hat Spur B)
- **Wartet auf:** Schritt 0 (ADR-0028, `IPointerFence`, Standbild).
- **Parallel zu:** Spur B (B1 Inseln) und Spur C (A6 Puffer und Achsensperre).
- **Merge-Reihenfolge:** A6 → **G2** → B1. G2 baut den Zaun mit den Puffer-Regeln von A6 zusammen.
  Beim Merge prüfen, dass Puffer und Sperre sich nicht überlagern: ein eingefrorener Monitor ist
  immer gesperrt, der Puffer gilt nur zwischen freien Monitoren.

## Akzeptanzkriterien
- [ ] `Strg+Alt+H` friert den Monitor unter dem Zeiger ein. Der Zeiger steht sofort auf dem
      nächstgelegenen freien Monitor.
- [ ] Der Zeiger erreicht einen eingefrorenen Monitor weder per Maus noch per `Strg+Alt+N`.
- [ ] Ein erneutes `Strg+Alt+H` hebt alle auf. Danach ist der Monitor wieder erreichbar und folgt
      dem Zeiger.
- [ ] Der letzte freie Monitor lässt sich nicht einfrieren, es kommt ein Hinweis. Mit einem Monitor
      bleibt das Kürzel ohne Wirkung und gibt einen Hinweis.
- [ ] Die Reihenfolge stimmt mit dem Grilling-Beispiel überein, auch wenn die Windows-Nummern nicht
      der Lage entsprechen.
- [ ] Live: Ein Video läuft auf dem eingefrorenen Monitor weiter. Standbild: Das Bild bleibt stehen.
- [ ] Nach einem Neustart ist kein Monitor eingefroren, und der Zeiger ist frei.
- [ ] Win+L, UAC und das Umstecken eines Monitors heben das Einfrieren auf oder stellen den Zaun
      korrekt wieder her. Der Zeiger ist danach nie eingesperrt.
- [ ] Rahmen und Inhalt sind in Bildschirmübertragungen unsichtbar.

## Offene Fragen / Risiken
- **Monitor verschwindet:** Wird ein eingefrorener Monitor abgesteckt, heben wir dort auf. Wird der
  letzte freie Monitor abgesteckt, heben wir alle auf, damit der Zeiger nie gefangen ist.
- **Nutzer vergessen das Einfrieren:** Rahmen und Ansage helfen. Optional ein Tray-Symbol im Zustand
  „eingefroren“ (beim Umsetzen klären).
- **Ziehen über eingefrorene Monitore:** Ein Fenster lässt sich nicht auf einen eingefrorenen Monitor
  ziehen, weil der Zaun den Zeiger stoppt. Gewollt, aber im Hinweistext erwähnen.
- **Lupe und Angedockt:** Einfrieren friert den jeweiligen Modus-Ausschnitt ein. Die Lupe bleibt an
  ihrer Stelle. Beim Umsetzen prüfen.
- **Abstand der Monitore:** Bei gleichem Abstand nach links und rechts gilt „rechts zuerst“.
  Gestapelte Monitore folgen der Reihenfolge oben → unten in derselben Spalte.
