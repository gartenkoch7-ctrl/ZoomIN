# B1 · Eingefrorene Ausschnitte („Inseln“)
**Meilenstein:** [M19](../m19-mehrere-ansichten.md) · **Spur:** B · **Branch:** `feat/m19-b1-inseln` ·
**Größe:** M · **Status:** geplant

**Ziel:** Bis zu vier feste Bildschirmbereiche (z. B. Uhr, Statuszeile, Chat, Formelzeile) bleiben
als vergrößerte Inseln über dem Zoom sichtbar, während der Zoom normal der Maus folgt.

**Demo:**
1. `Strg+Alt+Umschalt+I` startet den Bearbeitungsmodus.
2. Ein Rechteck um die Uhr aufziehen, mit dem Mausrad 3× einstellen, Esc beendet den Modus.
3. Die Uhr bleibt oben rechts als Insel stehen, während man bei 4× im Dokument arbeitet.
4. Ein Klick auf die Insel trifft das Dokument darunter.
5. `Strg+Alt+I` blendet alle Inseln aus und wieder ein.
6. Nach einem Neustart ist die Insel wieder da.

**Voraussetzungen:** M19 Schritt 0, insbesondere der Baustein „Standbild“. Diese Spur braucht
weder ADR-0028 noch den Zeigerzaun.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (B1) ·
Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md) ·
Grundsatz: [ADR-0028](../../adr/0028-zeiger-versetzen-nur-auf-befehl.md) (Inseln versetzen den Zeiger nie)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Anzahl | Bis zu 4 Inseln |
| Ein/Aus | `Strg+Alt+I` blendet alle Inseln ein und aus |
| Bearbeiten | `Strg+Alt+Umschalt+I` startet den Bearbeitungsmodus: Rechteck aufziehen = neue Insel; vorhandene ziehen oder Größe ändern; Mausrad = Stufe der Insel; Entf = löschen; Esc = fertig |
| Eingabe | Nur der Bearbeitungsmodus darf Mauseingaben abfangen. Im normalen Betrieb nie. |
| Klick auf Insel | Fällt durch auf den Desktop darunter. Die Insel ist reine Anzeige. |
| Inhalt | Ab Werk live, Standbild je Insel wählbar |
| Merken | Inseln (Lage, Größe, Quellbereich, Stufe, live/Standbild) werden im Profil gespeichert und sind nach einem Neustart wieder da |
| Startstufe | Eine neue Insel startet mit der aktuellen Stufe des Monitors *(selbst entschieden)* |

## Umsetzung
**Core**
- `Core/Islands/Island`: Monitor-Id, Quellrechteck und Zielrechteck (beide `PhysicalRect`), Stufe,
  live/Standbild.
- `Core/Islands/IslandSet`: unveränderliche Liste mit höchstens 4 Einträgen, `Volatile`-Tausch.
  Regeln: Zielrechteck im Monitor halten, Mindestgröße, Stufe in den `ZoomLimits`.
- `Core/Islands/IslandEditor`: reine Zustandsmaschine des Bearbeitungsmodus (Aufziehen, Ziehen,
  Ecke ziehen, Rad, Entf, Esc), testbar ohne Win32.
- Eine Insel zeigt einen Quellbereich vergrößert in einem Zielrechteck. Die Rechnung ist dieselbe
  wie bei der Lupe (`ModeGeometry`), nur mit festem statt mausgebundenem Fixpunkt.

**Rendering**
- `DesktopLayer`/`Magnify.hlsl` (Hotspot, nur diese Spur): Nach dem Hauptbild werden bis zu 4
  Inseln gezeichnet. Je Insel gibt es einen Draw-Aufruf mit Quell-UV und Zielrechteck aus einem
  Constant Buffer, der beim Start angelegt wird.
  - Farbfilter, Smart Invert und Glättung wirken wie beim Hauptbild.
  - Jede Insel hat einen Rahmen.
- Standbild: Insel mit Standbild nutzt den Baustein aus Schritt 0 und kopiert ihren Quellbereich
  einmal.
- Bearbeitungsmodus: Rahmen blinkt, Griffe an den Ecken, gestrichelter Rahmen beim Aufziehen.
- Alles im vorhandenen Overlay-Fenster, also mit `WDA_EXCLUDEFROMCAPTURE`. Kein neues Fenster.

**Platform**
- Bearbeitungsmodus: Der `WH_MOUSE_LL`-Hook aus `Win32InputActivityHook` bekommt einen
  zuschaltbaren Modus „abfangen“. Er gilt nur, solange der Bearbeitungsmodus läuft.
- Esc und Entf kommen über `WH_KEYBOARD_LL` und werden nur im Bearbeitungsmodus verschluckt.
- Sicherheitsnetz: Bearbeitungsmodus endet automatisch nach 2 min ohne Eingabe und bei einem
  Wechsel auf den sicheren Desktop *(selbst entschieden)*.

**App**
- Kürzel `ToggleIslands` (`Strg+Alt+I`) und `EditIslands` (`Strg+Alt+Umschalt+I`), anpassbar.
- `IslandController`: Kürzel, Editor, Hook, Speichern im Profil.
- `ProfileFields.ApplyDisplay`: Inseln gehören zum Profil.
- Seite „Zoom & Modi“: Karte „Inseln“ mit Liste (Monitor, Größe, Stufe, live/Standbild), Löschen
  und „Bearbeiten starten“.

## Aufgaben
- [ ] Core: `Island`, `IslandSet`, `IslandEditor` mit Tests (Abdeckung ≥ 90 %). Fälle: Obergrenze 4,
      Mindestgröße, Insel am Monitorrand, Stufe begrenzen, Esc bricht Aufziehen ab, Entf löscht.
- [ ] Rendering: Inseln im Desktop-Durchgang, Rahmen, Bearbeitungsdarstellung. WARP-Tests:
      Insel zeigt den richtigen Quellbereich, Filter wirken, Rahmenfarbe.
- [ ] Benchmark: Frame mit 4 Inseln `0 B`. GPU-Zeit mit 4 Inseln gegen ohne messen (RTX, ×10 hochgerechnet).
- [ ] Platform: abfangender Hook-Modus nur im Bearbeitungsmodus, automatisches Ende.
- [ ] App: Kürzel, Controller, Profil, Einstellungskarte.
- [ ] Telemetrie (ADR-0019):
  - Abläufe `zoomin.islands.edit` (Dauer, Zahl der Inseln danach) und `zoomin.islands.toggle`
  - Zähler `zoomin.islands.count`
  - Logs App 1740–1759, Rendering 3320–3339
  - Keine Inhalte, nur Rechtecke und Stufen
  - Katalog `docs/kb/telemetrie.md`
- [ ] Texte Deutsch/Englisch. Bildschirmleser sagt Bearbeitungsmodus an/aus und „Insel 2 von 4,
      3-fach“ an. Die Liste in den Einstellungen ist per Tastatur bedienbar.
- [ ] KB: `docs/kb/rendering-pipeline.md` (Inseln im Durchgang) und `docs/kb/eingabe-und-tracking.md`
      (Klicks fallen durch, Hook nur im Bearbeitungsmodus).
- [ ] Freigabetest: Inseln und Bearbeitungsrahmen sind in Teams/OBS unsichtbar.

## Parallelisierung
- **Gehört allein dieser Spur:**
  - `Core/Islands/*` und `IslandController`
  - Inselteil in `DesktopLayer.cs` und `Magnify.hlsl`. Nur diese Spur ändert die Shader-Teile.
  - der abfangende Modus im Maus-Hook
- **Hotspots, nur ergänzen:**
  - `AppSettings.cs`, `ProfileFields.cs`, `HotkeyGesture.cs`/`HotkeyBindings.cs`, `Texts*.cs`
  - `docs/kb/telemetrie.md`, `docs/kb/eingabe-und-tracking.md`
- **Wartet auf:** Schritt 0 (Baustein „Standbild“).
- **Parallel zu:** Spur A (G2) und Spur C (A6).
- **Merge-Reihenfolge:** A6 → G2 → **B1** (zuletzt).
  - Beim Merge prüfen: Inseln auf einem eingefrorenen Monitor (G2) bleiben sichtbar und frieren
    mit ein.
  - Der Maus-Hook ist zwischen A6 (Puffer, Achse) und B1 (Bearbeitungsmodus) geteilt. Seine
    Reihenfolge ist: Bearbeitungsmodus vor Achse vor Puffer.

## Akzeptanzkriterien
- [ ] Bis zu 4 Inseln lassen sich im Bearbeitungsmodus anlegen, verschieben, in der Größe ändern,
      in der Stufe ändern und löschen. Eine fünfte wird mit Hinweis abgelehnt.
- [ ] Außerhalb des Bearbeitungsmodus fängt ZoomIN keine Mausklicks ab. Ein Klick auf eine Insel
      trifft das Fenster darunter.
- [ ] `Strg+Alt+I` blendet alle Inseln ein und aus.
- [ ] Live-Inseln aktualisieren sich (Uhr läuft), Standbild-Inseln bleiben stehen.
- [ ] Nach einem Neustart und nach einem Profilwechsel erscheinen die Inseln des Profils.
- [ ] Farbfilter und Smart Invert wirken in Inseln wie im Hauptbild.
- [ ] Inseln und Rahmen sind in Bildschirmübertragungen unsichtbar.
- [ ] Bildrate bleibt bei 4 Inseln gleich der Monitor-Hz.

## Offene Fragen / Risiken
- **Layout ändert sich:** Inseln hängen an Bildschirmrechtecken, nicht an Steuerelementen. Bei
  verschobenen Fenstern zeigen sie den falschen Bereich (wie bei den Vorbildern). Ein Hinweis in
  der Hilfe reicht. An Fenster binden wäre eine spätere Idee.
- **Auflösung oder DPI ändert sich:** Inseln außerhalb des Monitors werden in den Monitor
  geschoben oder ausgeblendet. Nie Absturz.
- **Verdeckung:** Eine Insel über dem Zeiger kann ihn verdecken. Der Zeiger wird nach den Inseln
  gezeichnet, also immer oben.
- **Mehrere Monitore:** Eine Insel gehört zu genau einem Monitor. Quelle und Ziel auf verschiedenen
  Monitoren wären F1 (zweiter Monitor zeigt den ersten) und sind hier nicht enthalten.
- **Lupe und Angedockt:** Inseln gelten im Vollbild. Ob sie in der Lupe oder im Streifen sichtbar
  sind, beim Umsetzen klären. Vorschlag: nur Vollbild.
