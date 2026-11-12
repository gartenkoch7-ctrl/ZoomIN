# B1 · Eingefrorene Ausschnitte („Inseln“)
**Meilenstein:** [M19](../m19-mehrere-ansichten.md) · **Spur:** B · **Branch:** `feat/m19-b1-inseln` ·
**Größe:** M · **Status:** umgesetzt auf `feat/m19-b1-inseln` (2026-11-12), Sichtprüfung und Freigabetest offen

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
- [x] Core: `Island`, `IslandSet`, `IslandEditor` mit Tests (Abdeckung ≥ 90 %). Fälle: Obergrenze 4,
      Mindestgröße, Insel am Monitorrand, Stufe begrenzen, Esc bricht Aufziehen ab, Entf löscht.
- [x] Rendering: Inseln im Desktop-Durchgang, Rahmen, Bearbeitungsdarstellung. WARP-Tests:
      Insel zeigt den richtigen Quellbereich, Filter wirken, Rahmenfarbe.
- [x] Benchmark: Frame mit 4 Inseln `0 B`. GPU-Zeit mit 4 Inseln gegen ohne messen (RTX, ×10 hochgerechnet).
- [x] Platform: abfangender Hook-Modus nur im Bearbeitungsmodus, automatisches Ende.
- [x] App: Kürzel, Controller, Profil, Einstellungskarte.
- [x] Telemetrie (ADR-0019):
  - Abläufe `zoomin.islands.edit` (Dauer, Zahl der Inseln danach) und `zoomin.islands.toggle`
  - Zähler `zoomin.islands.count`
  - Logs App 1740–1759, Rendering 3320–3339
  - Keine Inhalte, nur Rechtecke und Stufen
  - Katalog `docs/kb/telemetrie.md`
- [x] Texte Deutsch/Englisch. Bildschirmleser sagt Bearbeitungsmodus an/aus und „Insel 2 von 4,
      3-fach“ an. Die Liste in den Einstellungen ist per Tastatur bedienbar.
- [x] KB: `docs/kb/rendering-pipeline.md` (Inseln im Durchgang) und `docs/kb/eingabe-und-tracking.md`
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
- [x] Bis zu 4 Inseln lassen sich im Bearbeitungsmodus anlegen, verschieben, in der Größe ändern,
      in der Stufe ändern und löschen. Eine fünfte wird mit Hinweis abgelehnt.
- [x] Außerhalb des Bearbeitungsmodus fängt ZoomIN keine Mausklicks ab. Ein Klick auf eine Insel
      trifft das Fenster darunter.
- [x] `Strg+Alt+I` blendet alle Inseln ein und aus.
- [x] Live-Inseln aktualisieren sich (Uhr läuft), Standbild-Inseln bleiben stehen.
- [x] Nach einem Neustart und nach einem Profilwechsel erscheinen die Inseln des Profils.
- [x] Farbfilter und Smart Invert wirken in Inseln wie im Hauptbild.
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
  → **Entschieden beim Umsetzen:** nur Vollbild. Bearbeiten geht auch in der Lupe (bei 1×), im
  angedockten Modus nicht (dort gibt es keine vorübergehende 1×).

## Umsetzungsstand (2026-11-12)
Details: [rendering-pipeline.md](../../kb/rendering-pipeline.md#inseln-m19-b1) und
[eingabe-und-tracking.md](../../kb/eingabe-und-tracking.md) (Abschnitt „Inseln“).
- **Abweichung Zeichnen:** eigene Klasse `Rendering/Islands/IslandLayer` (ein Draw je Insel mit dem
  vorhandenen `MagnifyPass`) statt Code in `DesktopLayer`; gezeichnet nach den Hervorhebungen, vor
  Übersichtsrahmen und Zeiger. `DesktopLayer.cs` blieb unverändert.
- **Shader:** `Magnify.hlsl` bekommt `StillOffset/StillScale` (Puffer b0 128 → 144 B) und tastet über
  `TexUv` ab, damit Standbilder nur ihren Quellbereich halten müssen. Live bitgleich wie vorher.
- **Bearbeiten bei 1×** (selbst entschieden): alle Monitore vorübergehend 1× (neuer Besitzer
  `TemporaryZoomOwner.IslandEdit`), damit Zeiger, Quelle und Inselbild dieselben Koordinaten haben.
- **Hook:** abfangender Modus `Platform/Input/IslandEditCapture` am vorhandenen Aktivitäts-Hook
  (Maus und Tastatur), kein neuer Hook. Zeigerzaun: Filter `IslandDragFence`
  (`PointerFilterOrder.IslandEdit`) hält ein Ziehen auf dem Monitor der Insel; gesperrte Bereiche
  (`FenceOwner.IslandEdit`) braucht B1 nicht.
- **Telemetrie:** App 1740–1747; Rendering 3320–3339 bleibt frei (kein Log im Render-Loop,
  Standbilder melden 3300/3301). GPU-Zeit im Betrieb als `zoomin.render.gpu_time` (`islands`).
- **GPU** (RTX 3060 Ti, 1080p, 4×, 4 Inseln 600 × 160): +0,008–0,018 ms, ×10 ≈ 0,2 ms.
- **Offen für den Menschen:** Sichtprüfung (Blinken, Griffe, gestricheltes Rechteck, Uhr läuft),
  Freigabetest Teams/OBS, Bildrate bei 4 Inseln am echten Monitor, Ansagen mit NVDA/Sprachausgabe.

## Akzeptierte Abwägungen (Review-Schleife §9)
Befunde der Reviews, die bewusst nicht als Code umgesetzt sind (Designvorschläge oder bekannte Grenzen):
- **Standbild und Live-Daten:** Gewichte des intelligenten Invertierens und Bereiche des Filters pro
  Fenster kommen vom Live-Bild, nicht vom Standbild.
- **Standbild-Zeitpunkt:** aufgenommen beim ersten Zeichnen nach dem Umschalten, nach Neustart oder
  Neuaufbau der Pipeline; freigegeben beim nächsten Frame des Monitors (schläft er, beim Aufwachen).
- **Anzeigewechsel zur Laufzeit:** Inseln werden beim Start, beim Profilwechsel und beim Bearbeiten
  eingepasst, nicht bei jedem Anzeigewechsel (kein Monitor-Ereignis in Core). Bis dahin ausgeblendet
  bzw. für den kleineren Monitor eingepasst gezeichnet; gespeichert bleibt das Original.
- **Verlorener Aktivitäts-Hook:** Entfernt Windows den Hook stillschweigend, merkt ZoomIN das nicht;
  das 2-min-Sicherheitsnetz beendet den Modus.
- **Zaun klemmt am Rand:** Die vom Zaun per `SetCursorPos` gesetzte Position sieht kein Low-Level-Hook;
  die Insel folgt bis zum Loslassen nur bis zur letzten gesehenen Position.
- **Halbe Pixel:** Bei Stufen wie 2,5× kann die Gegenkante beim Ecke-Ziehen einen halben Pixel springen.
- **Esc beim Start losgelassen:** Ohne zweiten Blick (wie bei der Übersicht) kann das erste Esc an die
  Anwendung gehen, wenn es genau beim Start losgelassen wurde.
- **Architektur:** Zwei Sperren (Start/Ende, Eingabeschritt) statt eines einzigen Akteurs; Enden aus
  fremden Threads laufen über den Thread-Pool. Ein Umbau zum Akteur wäre sauberer, ist aber kein Fehler.
- **Kosten im Bearbeitungsmodus:** Jede Bewegung setzt den Leerlauf-Timer neu und vergleicht die
  Szene; das gestrichelte Rechteck sind bis zu ~260 Rechteck-Draws. Nur während des Bearbeitens.
- **Doppelter Code:** Monitornummer aus `\\.\DISPLAYn` (Zoom- und Filterseite), Punkt-Klemmen
  (`IslandEditor`, `ModeGeometry`, `FenceGeometry`), Tasten-Wiederholungslücke (`OverviewKeyWatch`).
  `PointerStyle` nutzt 1,01 noch als Zahl statt `ZoomLimits.UnzoomedUpTo`.
- **P/Invoke in `Platform/Input`:** `GetSystemMetrics` wie die übrigen Hook-Klassen dort direkt über
  CsWin32 (gelebte Ausnahme der Regel „nur in Interop/“).
- **Monitorwechsel bei offener Einstellungsseite:** Die Zeilen aktualisieren Monitornummer und
  „nicht angeschlossen“ erst bei der nächsten gespeicherten Änderung oder beim erneuten Öffnen.
- **`PointerStyle`** nutzt 1,01 noch als Zahl; `ZoomLimits.UnzoomedUpTo` gilt für Inseln und Overlay.
- **Dateigröße:** `IslandController.cs`, `IslandEditor.cs` und
  `IslandEditorTests.cs` liegen knapp unter 250 Zeilen.
- **Einpassen im Modell:** Gezeichnete (eingepasste) und gespeicherte Inseln werden im Modell getrennt
  geführt (`Committed`, `WithUntouchedFrom`). Einpassen erst beim Zeichnen und Greifen wäre einfacher
  und würde auch nach Anzeigewechseln wirken – späterer Umbau.
- **`RenderModels.Islands` mit Vorgabewert:** wie `Markers`; die Composition Root setzt das echte Modell.
- **Rad-Rest:** Teil-Rastungen des Touchpads sammeln sich je Lauf und Richtung, nicht je Insel oder
  Pause; eine halbe Rastung kann sich mit einer späteren halben auf einer anderen Insel addieren.
- **Überflüssiges Speichern:** Endet ein Lauf mit Änderungen, nachdem eine Änderung von außen gewann,
  wird einmal unverändert gespeichert.
