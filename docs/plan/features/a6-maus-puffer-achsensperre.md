# A6 · Maus-Puffer und Achsensperre
**Meilenstein:** [M19](../m19-mehrere-ansichten.md) · **Spur:** C · **Branch:** `feat/m19-a6-maus-puffer` ·
**Größe:** S · **Status:** geplant

**Ziel:**
- Der Zeiger rutscht nicht mehr versehentlich auf den anderen Monitor.
- Mit einer gehaltenen Taste bewegt er sich nur waagrecht oder senkrecht, zum Beispiel zum Lesen
  von Tabellenzeilen.

**Demo:**
- **Puffer:** Zwei Monitore, Puffer „Mittel“. Die Maus bleibt am Monitorrand stehen. Erst wer etwa
  0,4 s gegen den Rand drückt, kommt hinüber.
- **Achsensperre:** Haltetaste „Umschalt links“ ist gewählt. Taste halten und nach rechts bewegen:
  Der Zeiger läuft genau waagrecht die Tabellenzeile entlang, auch wenn die Hand leicht nach unten
  zieht. Taste loslassen, und die Bewegung ist wieder frei.

**Voraussetzungen:** M19 Schritt 0, nur der Baustein „Zeigerzaun“ (`IPointerFence`). Der Zeiger
wird hier nicht versetzt, nur gebremst bzw. begrenzt. ADR-0028 ist trotzdem zu beachten.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (A6) ·
Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md) ·
Grundsatz: [ADR-0028](../../adr/0028-zeiger-versetzen-nur-auf-befehl.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Puffer | Ab Werk aus. Stufen Leicht · Mittel · Stark ≈ 0,2 s / 0,4 s / 0,8 s gegen den Rand drücken, bevor der Zeiger hinüber darf. |
| Achsensperre | Ab Werk aus. Ausgelöst durch eine gehaltene Taste, die der Nutzer wählt (z. B. Umschalt links). Die erste Bewegung legt waagrecht oder senkrecht fest. |
| Geltung | Beide gelten global, nicht im Profil. |
| Puffer und Einfrieren | Ein eingefrorener Monitor (G2) ist immer gesperrt. Der Puffer gilt nur zwischen freien Monitoren. *(selbst entschieden)* |
| Richtung | Nach etwa 8 px Bewegung entscheidet die größere Achse. Bis dahin ist die Bewegung frei. *(selbst entschieden)* |

## Umsetzung
**Core**
- `Core/Pointer/EdgeBuffer` (rein): Aus Zeigerposition, Monitorrechtecken, Stufe und Zeit (über
  `TimeProvider` vom Aufrufer) entscheidet sie, ob eine Bewegung über eine Monitorgrenze erlaubt
  ist. „Drücken“ heißt: Der Zeiger steht am Rand, und weitere Bewegungen zeigen nach außen.
  Ein Rückzug setzt den Zähler zurück.
- `Core/Pointer/AxisLock` (rein): Aus Startpunkt, gesammelter Bewegung und Schwelle (8 px) wählt sie
  die Achse und liefert die begrenzte Position.
- Beide ohne Allokation, Tests für alle Grenzfälle.

**Platform**
- Der `WH_MOUSE_LL`-Hook aus `Win32InputActivityHook` bekommt einen Modus „begrenzen“:
  - **Puffer:** Bewegungen über eine Monitorgrenze werden verschluckt, bis `EdgeBuffer` sie
    freigibt. Über `IPointerFence` aus Schritt 0, das für mehrere Monitore schon den Hook nutzt.
  - **Achse:** Ist die Achse gewählt, bekommt der Zeiger per `ClipCursor` ein 1 px hohes bzw.
    breites Band über den Monitor. Beim Loslassen der Taste wird das Band gelöst.
    `ClipCursor` gilt nur, solange die Taste gehalten wird.
- Haltetaste über `WH_KEYBOARD_LL` (nur lesen, nichts verschlucken), wie die Strg-Erkennung der
  Suchhilfe. Unterschieden werden linke und rechte Taste (`VK_LSHIFT`/`VK_RSHIFT` usw.).
- Neu anwenden nach einem Desktopwechsel, Win+L, UAC und einer Anzeigeänderung, weil Windows
  `ClipCursor` zurücksetzt. Das macht der Zaun aus Schritt 0.

**App**
- `PointerConstraintController`: Einstellungen → Hook-Modus, Taste → Achse.
- Seite „Mauszeiger“, Karte „Bewegung begrenzen“:
  - Puffer: Aus / Leicht / Mittel / Stark
  - Achsensperre: an/aus, Haltetaste (Umschalt, Strg, Alt, je links/rechts)
  - Hinweis, dass Umschalt+Ziehen in vielen Programmen Text markiert

## Aufgaben
- [ ] Core: `EdgeBuffer` und `AxisLock` mit Tests (Abdeckung ≥ 90 %). Fälle: alle drei Stufen,
      Rückzug setzt zurück, Ecke zwischen drei Monitoren, Monitore mit Versatz, Achse nach 8 px,
      Diagonale, Taste los.
- [ ] Benchmark: Hook-Pfad je Mausbewegung `0 B` (`EdgeBuffer`, `AxisLock`).
- [ ] Platform: Hook-Modus „begrenzen“, Band per `ClipCursor`, linke/rechte Taste unterscheiden,
      neu anwenden nach Desktopwechsel.
- [ ] App: Controller, Einstellungskarte, Werkswerte aus.
- [ ] Telemetrie (ADR-0019):
  - Zähler `zoomin.pointer.buffer_blocked` (gestoppte Übergänge) und `zoomin.pointer.axis_lock`
    (Nutzungen, Tag Achse)
  - Logs App 1760–1779, Platform 2960–2979
  - Keine Koordinaten pro Bewegung loggen
  - Katalog `docs/kb/telemetrie.md`
- [ ] Texte Deutsch/Englisch. Bildschirmleser liest die Karte vollständig, die Stufen haben
      verständliche Namen.
- [ ] KB: `docs/kb/eingabe-und-tracking.md` (Puffer, Achse, ClipCursor-Rücksetzung durch Windows).

## Parallelisierung
- **Gehört allein dieser Spur:**
  - `Core/Pointer/EdgeBuffer`, `Core/Pointer/AxisLock`, `PointerConstraintController`
  - der Modus „begrenzen“ im Maus-Hook
- **Hotspots, nur ergänzen:**
  - `AppSettings.cs` (globale Werte, **nicht** in `ProfileFields.cs`), `Texts*.cs`
  - `docs/kb/telemetrie.md`, `docs/kb/eingabe-und-tracking.md`
  - `Win32InputActivityHook` wird mit B1 geteilt: B1 fügt den Bearbeitungsmodus hinzu, A6 den
    Modus „begrenzen“. Beide nur als eigene Methoden bzw. Klassen ergänzen.
- **Wartet auf:** Schritt 0 (`IPointerFence`).
- **Parallel zu:** Spur A (G2) und Spur B (B1).
- **Merge-Reihenfolge:** **A6** → G2 → B1 (zuerst, weil am kleinsten). G2 und B1 bauen auf dem
  Hook-Zustand von A6 auf. Reihenfolge im Hook: Bearbeitungsmodus (B1) vor Achse vor Puffer.

## Akzeptanzkriterien
- [ ] Puffer aus: Verhalten wie Windows.
- [ ] Puffer an: Der Zeiger bleibt am Rand stehen und kommt erst nach der Druckzeit der Stufe hinüber.
- [ ] Ein eingefrorener Monitor (nach dem Merge mit G2) bleibt unabhängig vom Puffer gesperrt.
- [ ] Achsensperre: Mit gehaltener Taste bewegt sich der Zeiger nur auf einer Achse, beim Loslassen
      ist er sofort frei.
- [ ] Nach Win+L, UAC und Abstecken eines Monitors ist der Zeiger nie eingesperrt.
- [ ] Mausbewegung bleibt flüssig: kein spürbarer Versatz, Hook-Pfad ohne Allokation.

## Offene Fragen / Risiken
- **Hook-Zeitlimit:** Windows entfernt einen `WH_MOUSE_LL`-Hook, der zu langsam antwortet
  (`LowLevelHooksTimeout`). Der Hook darf deshalb nur rechnen, nie blockieren oder loggen.
  Telemetrie läuft gebündelt außerhalb.
- **Spiele und Fernsteuerung:** Programme mit eigenem `ClipCursor` (Spiele, Remote Desktop) können
  mit der Achsensperre kollidieren. Die Sperre ist nur aktiv, solange die Taste gehalten wird.
  Beim Umsetzen prüfen.
- **Umschalt+Ziehen:** Hält man Umschalt als Achsentaste und zieht, markieren viele Programme Text.
  Darauf weist die Einstellungskarte hin. Die Haltetaste ist frei wählbar.
- **Puffer und Lupenrand:** Der Puffer wirkt nur an Monitorgrenzen, nicht am Rand der Lupe oder
  des Streifens.
