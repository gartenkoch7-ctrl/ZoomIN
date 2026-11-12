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

**Tatsächlich (2026-11-12):** Der Integrationsbranch enthält vor den Spuren auch die Merges von M17
(`feat/m17-ruhige-ansicht`) und M18 (`feat/m18-lesbarkeit`, zweimal nachgezogen), weil M19 auf beiden
aufbaut. Danach A6 (Merge in den Integrationsbranch) → G2 (Integrationsbranch mit M18 und A6 in G2
gemergt, G2 auf dem Integrationsbranch fertiggestellt) → B1 (Integrationsbranch mit M18, A6 und G2 in
B1 gemergt, Merge-Review in sechs Runden, Integrationsbranch auf B1 vorgezogen). Ein Merge nach
`develop` bringt deshalb M17, M18 und M19 zusammen, sofern M17/M18 nicht vorher dort landen.

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
- [x] ADR-0028 liegt vor; Integrationsbranch anlegen, Schritt 0 mit Tests, Review (§9), Commit
      (Zaun: `IPointerFence`/`FenceGeometry`/`IPointerMoveFilter`, `IPointerMover`; Standbild:
      `Rendering/Still/StillFrame`; Watchdog löst `ClipCursor` nach Absturz)
- [x] Spuren A, B, C gleichzeitig starten (Worktree je Spur)
- [x] Mergen C → A → B, nach jedem Merge Build + Tests (siehe „Tatsächlich“ oben)
- [x] Gesamt-Review (§9) bis ohne Befunde: 13 Runden, die letzte ohne Code-Änderung (siehe „Integration“ unten)
- [x] Dateien über 250 Zeilen nach Verantwortung aufgeteilt: `RenderSession` → `SessionPointer`
      (Zeiger und Anker je Frame, jetzt 236 Zeilen), `MonitorFreezeController` → `FreezeLifter` (Aufheben
      und Fläche freigeben) und `FreezeRefusals` (Gründe gegen das Einfrieren), jetzt 219 Zeilen. Die
      Tests der Ablehnungen stehen in `FreezeRefusalTests`. `AnchorController` (249) und `ZoomModel` (247) bleiben: kein sauberer Schnitt
      (Tastatur-Verfolgung und Gleiten teilen dieselben Felder; das Modell ist schon in
      `TemporaryZoom`/`ParkedTargets` geteilt), beide unter 250.
- [x] Wechselwirkungen prüfen:
  - [x] Puffer am Rand zu einem eingefrorenen Monitor (`FrozenMonitorChainTests`: der Puffer gibt nach,
        die Sperre nicht; zwischen freien Monitoren wirkt er weiter; Achsensperre hält die Zeile)
  - [x] Inseln auf einem eingefrorenen Monitor und Bearbeiten bei eingefrorenem Monitor
        (`IslandFreezeTests`, `FrozenIslandsTests`, Merge-Review B1)
  - [x] Übersicht (M17 B2) und Fenster-Verschieben (M17 G1) bei eingefrorenem Monitor: Einfrieren
        während einer Übersicht oder einer anderen vorübergehenden Stufe (Fenster verschieben oder Größe ziehen) auf dem Monitor wird abgelehnt (Hinweis); auf
        einem eingefrorenen Monitor wirken beide nicht (`MonitorRenderer` nimmt die Stufe des
        Einfrierens, nur per Review belegt). Eine Stufe des Verschiebens gilt nach dem Auftauen bis zum
        Loslassen (`ParkedTargetsTests.AWindowDragLevel_…`, `FreezeRefusalTests`)
  - [x] Sperren mit Win+L, UAC und sichere Bildschirme heben Einfrieren, Bearbeiten und gehaltene
        Achsentaste gemeinsam auf; Abstecken des eingefrorenen Monitors hebt ihn auf und beendet das
        Bearbeiten, die Achsentaste bleibt (`FenceOwnersTogetherTests`, gefälschter Zaun)
  - [x] `Recomputed` beim Loslassen von selbst: nicht gemeldet, dokumentiert (Kompromisse unten)
  - [x] `LandingPoint` und andere Zaun-Besitzer: nur G2 sperrt Bereiche (`Forbid` nur mit
        `FrozenMonitors`), A6/B1 nutzen Filter; `LandingPoint` mit den eingefrorenen reicht
  - [x] Tastatur-Hooks (A6 Haltetaste, M18 Haltetaste, B1/B2 Aktivitäts-Hook): nur Esc/Entf werden
        verschluckt, Haltetasten nie (Review; `IslandEditCaptureTests.HoldKeys_AlwaysPass_WhileEditing`
        bewacht B1). Maus-Hooks siehe Kompromisse.
  - [x] EventIds eindeutig und in den Spurbereichen (`TelemetryConventionTests`), `IPointerMover` nur
        in `*Controller.cs` (`PointerMoverCallerTests`)
- [ ] Test mit drei Monitoren (virtuelle Monitore, M3-Treiber) inkl. Lage 1 · 3 · 2
- [ ] Sichtprüfung per KI-Debug-Schnappschuss, danach Nutzer
- [ ] Release-Notes 1.3 (offen); [x] Status hier und in der Roadmap; [x] Merge nach `develop` mit Freigabe (2026-11-12)

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

## Integration (2026-11-12)
Gesamt-Review über die M19-Änderung (`origin/feat/m18-lesbarkeit...HEAD`, M17/M18 schon geprüft).

**Behoben:**
- Inselkürzel (`Strg+Alt+I`, `Strg+Alt+Umschalt+I`) kamen wie die Einfrierkürzel ohne Schema-Wechsel,
  wurden aber nicht gegen ältere Kürzel geprüft: Wer `Strg+Alt+I` schon vergeben hatte, sah das Inselkürzel
  bei jedem Start als „nicht verfügbar“. `SettingsMigration.WithoutNewHotkeyClashes` prüft jetzt alle vier
  M19-Kürzel; ein selbst aufgenommenes M19-Kürzel geht dem Werkskürzel eines anderen vor.
- Einfrieren während eines Fenster-Verschiebens oder einer Größenänderung mit eigener Stufe (M17 G1): wird jetzt
  abgelehnt (Hinweis „Erst das Verschieben oder die Größenänderung des Fensters beenden“, `window_drag`),
  wie bei der Übersicht. Sonst nahm der Sprung des Zeigers das gehaltene Fenster mit, und ein gesenktes
  Bild sprang auf die Stufe des Nutzers. Senkt eine andere vorübergehende Stufe den Monitor (z. B. eine
  gerade endende Übersicht), heißt der Hinweis „Der Monitor zeigt gerade eine vorübergehende Ansicht“
  (`temporary_level`).
- Aufteilung `RenderSession` → `SessionPointer`, `MonitorFreezeController` → `FreezeLifter` und
  `FreezeRefusals` (Verhalten gleich bis auf die neue Ablehnung oben und das Loggen eines Zaunfehlers nach
  gescheitertem Auftauen). Benchmarks alle `0 B` (26, `--job short --memory`); `SessionPointer` hat
  keinen eigenen Benchmark, die Rechnung darin ist `AnchorController` (`AnchorCenterMouse`,
  `AnchorMargin`, `AnchorFollowCaret`), dazu kommen nur Feldzuweisungen.

### Integration: angenommene Kompromisse
- **Zaun lässt von selbst los, ohne `Recomputed`:** Scheitert der Prüftakt, verweigert Windows einen neuen
  Hook oder scheitert `Forbid`, gibt der Zaun den Zeiger frei, meldet das aber nicht. Ein Melden wurde
  probiert und verworfen: Kein Besitzer kann etwas tun (G2 verlässt sich auf `pointer_inside`, A6 und B1
  haben nichts zu sperren), der Prüftakt holt den Hook zurück. Fällt der Zaun nur von Hook auf Clip zurück,
  fallen die Filter (Puffer, Achse, Zieh-Zaun) bis dahin still aus. Details in
  [eingabe-und-tracking.md](../kb/eingabe-und-tracking.md#zeigerzaun-und-zeiger-versetzen-m19-schritt-0-adr-0028).
- **`Strg+Alt+N` beim Bearbeiten der Inseln:** bleibt erlaubt (Inseln auf einem anderen Monitor
  bearbeiten). Während eines Ziehens holt die nächste Bewegung den Zeiger an den Rand des Zieh-Monitors
  zurück, die Ansage nennt trotzdem den Zielmonitor (Kompromiss aus B1).
- **Drei gleichlautende Prüfungen der Zaun-Auslöser** (`FreezeFenceEvents`, `IslandEditWatch`,
  `PointerConstraintController` mit Desktopwechsel/Entsperren/Anzeigewechsel): je Besitzer leicht anders
  (A6 liest bei Anzeigewechsel nur Monitore neu); ein gemeinsamer Helfer lohnt erst bei einem neuen Auslöser.
- **Maus-Hooks: ein geänderter Zug erreicht den Aktivitäts-Hook nicht.** Ändert der Zaun-Hook einen Zug
  (Achsensperre mit Zittern quer zur Achse, Drücken gegen Puffer, Sperre oder Zieh-Zaun), verschluckt er
  ihn und setzt den Zeiger mit `SetCursorPos`, das kein Low-Level-Hook sieht. Läuft der Zaun-Hook vor dem
  Aktivitäts-Hook (später installiert, Windows ruft den neuesten zuerst), sehen Suchhilfe (Schütteln),
  Ausblenden beim Tippen, Markierungen (E1 „Maus“) und das Ziehen einer Insel diese Züge nicht. Züge
  ohne Änderung (die ersten 8 px der Achse, Züge genau auf der Achse) kommen an, daher erscheint ein beim
  Tippen versteckter Zeiger wieder; eine Insel folgt mit gehaltener Achsentaste ruckelnd und stimmt beim
  Loslassen. Abhilfe wäre, dass der Zaun-Hook geänderte Züge an den Aktivitäts-Hook weitergibt.
- **Gehaltene Achsentaste beim Bearbeiten der Inseln:** Esc, Entf und Mausrad mit Umschalt, Strg oder
  Alt gehören dem System (B1-Regel). Ist die Achsentaste gehalten, wirken sie daher nicht im
  Bearbeitungsmodus, sondern gehen an die Anwendung. Erst die Taste loslassen.
- **Kürzel-Konflikte zur Laufzeit:** Die Seite „Tastenkürzel“ prüft keine Doppelbelegung. Nimmt der
  Nutzer für `FreezeMonitor` das Werkskürzel der Inseln auf, gewinnt bis zum Neustart die
  Registrier-Reihenfolge (Inseln), danach `WithoutNewHotkeyClashes` (aufgenommenes Kürzel). Kein Fehler
  im Ablauf, nur „nicht verfügbar“ bis zum Neustart.
- **Kürzel-Migration als Liste:** `WithoutNewHotkeyClashes` nennt die M19-Kürzel einzeln (wie die
  Übersicht in Schema 2). Ein allgemeiner Lauf über alle Kürzel mit „eingeführt in Version“ wäre
  robuster; jeder neue Meilenstein mit Kürzeln muss die Liste ergänzen.
- **Verschieben ohne eigene Stufe:** Ein Fenster-Verschieben ohne vorübergehende Stufe (Zoom aus, Art
  abgeschaltet, Angedockt) oder im Augenblick des Monitorwechsels sieht die Ablehnung nicht. Dann – wie
  bei `Strg+Alt+N` während eines Verschiebens – nimmt der Sprung des Zeigers das gehaltene Fenster mit
  auf den Zielmonitor. Beides ist ein ausdrücklicher Befehl; ZoomIN kennt keinen allgemeinen
  „Fenster wird gezogen“-Zustand.
- **`pointer_inside` allokiert einmal:** Sieht ein Render-Thread den Zeiger auf einem eingefrorenen
  Monitor (nur bei versagtem Zaun), reicht er das einmal je Ankunft per Thread-Pool an den Controller
  (Closure). Kein Pfad je Frame; ein vorab angelegter Arbeitsauftrag wäre `0 B`, lohnt aber nicht.
- **Hängende Stufe des Verschiebens:** Verpasst G1 das Ende eines Verschiebens, bleibt dessen Stufe bis
  zum nächsten Verschieben oder Anzeigewechsel stehen (G1-Verhalten); so lange lehnt `Strg+Alt+H` diesen
  Monitor mit dem Verschiebe-Hinweis ab.
- **Zwei Haltetasten-Erkennungen:** `Platform/Input/HoldKeyCodes` (M18, Markierungen) und
  `Platform/PointerConstraint/HoldKeyCodes` (A6, Achse) ordnen Tasten links/rechts zu und filtern von
  Windows erfundene Ereignisse unterschiedlich: Nur A6 kennt die unechten Umschalt-Ereignisse um
  Ziffernblocktasten bei NumLock. Mit „Umschalt links“ als Haltetaste der Markierungen kann eine
  Ziffernblock-Pfeiltaste die Markierung daher kurz verschwinden lassen, während die Achse hält. Das ist
  M18-Verhalten (vor M19 so geprüft); ein gemeinsamer Helfer für beide Hooks ist ein Folgeumbau.
- **Ablehnen statt angezeigte Stufe einfrieren:** `FrozenMonitor` kennt nur eine Stufe (die des Nutzers),
  die zugleich gezeigt und beim Auftauen zurückgegeben wird. Deshalb lehnt G2 das Einfrieren unter einer
  senkenden vorübergehenden Stufe ab. Eine getrennte „gezeigte Stufe“ würde stattdessen genau das Bild
  einfrieren; jeder neue Besitzer einer vorübergehenden Stufe ist bis dahin nur über die allgemeine
  Prüfung (vorübergehende Obergrenze unter der Stufe des Nutzers, neutraler Hinweis) abgedeckt.
- **Auftau-Fehler doppelt abgesichert:** Scheitert ein Empfänger des Auftauens, gibt `FreezeLifter` die
  Fläche selbst frei (unabhängig vom Aufrufer), danach gibt `FreezeGuard` noch einmal alles frei. Das sind
  zwei Zaun-Aufrufe in einem seltenen Fehlerfall; scheitert auch der Zaun, steht 1725 (`release`) zweimal
  im Log, der ursprüngliche Fehler einmal.
- **Hinweis bei gleichzeitig beginnendem Verschieben:** `FreezeRefusals` liest erst die Stufe des
  Verschiebens, dann die gezeigte Stufe. Beginnt ein Verschieben genau dazwischen, wird richtig abgelehnt,
  aber mit dem neutralen Hinweis (`temporary_level`).
- **Test der Haltetasten im Bearbeitungsmodus ist schwach:** `HoldKeys_AlwaysPass_WhileEditing` hält nur
  fest, dass B1 Umschalt/Strg/Alt nicht verschluckt (ohne gedrückten Modifier). Das Zusammenspiel der Hooks
  ist per Review geprüft, nicht per Test (echte Hooks greifen nie in die Sitzung ein).
- **`WithoutNewHotkeyClashes` gibt immer eine Kopie zurück:** einmal je Laden, kein Aufrufer prüft die
  Identität (`SettingsLoader`); ein Vergleich der vier Kürzel vorab war eine dritte Liste derselben Kürzel.
- **Verschieben beginnt genau beim Einfrieren:** Die Gründe gegen das Einfrieren werden einmal vor dem
  Sprung des Zeigers geprüft. Beginnt ein Verschieben zwischen Prüfung und Sprung, nimmt der Sprung das
  Fenster mit (wie bei „Verschieben ohne eigene Stufe“).
- **`AnchorController` (249) und `ZoomModel` (247)** bleiben ungeteilt (kein sauberer Schnitt, siehe
  Aufgaben). Wer sie erweitert, teilt zuerst.
- Die Kompromisse der Spuren stehen in ihren Feature-Plänen (B1: „Akzeptierte Abwägungen“; A6 und G2:
  „Offene Fragen / Risiken“ und KB).

### Braucht einen Menschen
- Sichtprüfung am echten Gerät: Rahmen des eingefrorenen Monitors, Inseln (Blinken, Griffe,
  gestricheltes Rechteck, Uhr läuft), Live-Video auf eingefrorenem Monitor, Standbild steht.
- Freigabetest Teams/OBS: eingefrorener Monitor, Rahmen, Inseln und Bearbeitungsrahmen unsichtbar.
- Drei Monitore (virtuell, M3-Treiber) in Lage 1 · 3 · 2: Grilling-Demo `Strg+Alt+H`/`Strg+Alt+N`.
- Echte Maus: Puffer-Stufen fühlbar, Achsensperre mit Umschalt/Strg/Alt links und rechts, kein spürbarer
  Versatz; Puffer am Rand eines eingefrorenen Monitors.
- Win+L, Strg+Alt+Entf, UAC und Abstecken eines Monitors bei eingefrorenem Monitor, laufendem
  Bearbeiten und gehaltener Achsentaste: Zeiger danach frei, nichts klemmt.
- Bildrate bei vier Inseln gleich der Monitor-Hz; Ansagen mit NVDA/Sprachausgabe.
- Release-Notes 1.3 und Merge nach `develop` (nur mit Freigabe).
