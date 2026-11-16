# A1 · Ruhige Mausverfolgung mit Randabstand
**Meilenstein:** [M17](../m17-ruhige-ansicht.md) · **Spur:** A · **Branch:** `feat/m17-a1-randabstand` · **Größe:** S–M · **Status:** umgesetzt, Sichtprüfung offen

**Ziel:** Das vergrößerte Bild bewegt sich erst, wenn der Mauszeiger einen einstellbaren Rand
erreicht. Dann schiebt es sich genau so weit, dass der Zeiger auf dem Randabstand bleibt. Das
bedeutet weniger Bildbewegung und damit weniger Schwindel.
**Demo:** Vollbild 4×, Maus in der Bildmitte hin und her → das Bild steht still. Maus zum rechten
Rand → das Bild läuft gleichmäßig mit, der Zeiger bleibt bei 20 % vom Rand stehen.
**Voraussetzungen:** keine. Die Spur startet sofort und braucht Schritt 0 nicht.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (A1), Bewertung:
[feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Modi | Mitte · **Randabstand** · Fixpunkt (proportional); ersetzt den Schalter `CenterMouse` |
| Werkswert | Randabstand |
| Bewegung am Rand | exakt mitschieben, wie beim Textcursor; keine Verzögerung, kein Springen |
| Abstand | Regler 0–50 %, Werk 20 % (0 % = erst ganz am Rand) |
| Bestehende Installationen | beim ersten Start nach dem Update **einmal** auf Randabstand umstellen, mit Hinweis, wo man zurückstellt; danach gilt die Wahl des Nutzers |
| Profil | Modus und Abstand gehören zum Profil |
| Gilt für | Vollbild und Lupe; Angedockt unverändert *(selbst entschieden)* |
| Fokus/Caret | unverändert (Fokus mittig, Caret 20 %); feinere Fokus-Verfolgung ist A5, nicht Teil von M17 *(selbst entschieden)* |

## Umsetzung
- **Core**
  - Neues Enum `MouseFollowMode { Center, Margin, Fixpoint }` in `Core/Tracking/`. Es ersetzt
    `TrackingOptions.CenterMouse`, dazu kommt `MouseMargin` (0–0,5).
  - `AnchorController` bekommt für `Margin` eine Schieberegel. Sie rechnet wie der Caret mit
    `CaretMargin`: Liegt der angezeigte Zeiger innerhalb des Randabstands, bleibt der Anker. Sonst
    wird der Anker so verschoben, dass der Zeiger genau auf dem Abstand steht.
  - `ViewCentering` bleibt für `Center`. Am Monitorrand bleibt die Ansicht stehen, wie heute.
  - Der Rückweg von Fokus/Caret zur Maus (nach 8 px Bewegung) übernimmt den aktuellen Anker,
    damit nichts springt.
  - Klicks treffen weiter, weil ZoomIN den Zeiger an seinem angezeigten Desktop-Punkt zeichnet
    (ADR-0018).
- **Settings:**
  - `AppSettings` bekommt ein neues Feld `MouseFollow` + `MouseMargin` und eine
    Migrationsmarke (z. B. `SettingsVersion` oder `MouseFollowMigrated`).
  - Die Migration beim Laden setzt einmalig `Margin` und merkt, dass der Hinweis fällig ist.
    Altes `CenterMouse` wird gelesen, aber nicht mehr geschrieben.
  - `ProfileFields.ApplyDisplay` und `TrackingMapping` werden ergänzt.
- **App**
  - Seite „Fokus & Verfolgung“: Auswahl Maus-Modus und Regler Randabstand (Wert und Einheit,
    ausgegraut außer bei Randabstand).
  - Einmaliger Hinweis nach der Migration (Windows-Benachrichtigung mit Link zur Seite).
- **Platform/Rendering:** keine Änderung außer dem neuen Feld im Snapshot.

## Aufgaben
### Core
- [x] `MouseFollowMode`, `TrackingOptions` umbauen (`CenterMouse` → `MouseFollow`, `MouseMargin`)
- [x] Schieberegel im `AnchorController` (Rechnung in `MouseFollowTarget`); Tests für Mitte,
      Schieben an allen vier Rändern, Monitorrand ohne Zittern, Zoomwechsel, Rückkehr von
      Fokus/Caret, Lupe (`MouseMarginTests`)
- [x] Klickpunkt-Test analog `MouseCenteringTests`: Zeiger wird am angezeigten Desktop-Punkt
      gezeichnet, über den ganzen Monitor (`MouseMarginClickTests`, auch mit Zoomwechseln/Sprüngen)
- [x] Benchmark `AnchorMargin` (neben `AnchorCenterMouse`): `0 B`, Zeit in `performance.md`
- [x] Abdeckung Core ≥ 90 % (94 %; `AnchorController`, `PlacementTracker`, `SettingsMigration` 100 %)
- [x] Für B2: `ViewPlacement` in `AnchorInput` (Anker an einen Punkt bei Zielzoom setzen), Tests
      `ViewPlacementTests`
### Settings & Migration
- [x] Felder, Werkswerte, `ProfileFields`, `TrackingMapping`, `SecureSettingsSanitizer`
- [x] Migration einmalig (`SchemaVersion` 1 → 2, `SettingsMigration` + `SettingsLoader`), Tests:
      alte Datei mit `CenterMouse=true/false`, neue Datei, zweiter Start ändert nichts
### App
- [x] Karte auf „Fokus & Textcursor“ (Auswahl „Maus folgen“ + Regler „Randabstand für die Maus“,
      ausgegraut außer bei Randabstand), Texte DE/EN, Bildschirmleser liest Wert + Einheit
      (`ValueSlider`/`SettingCard` wie bisher)
- [x] Hinweis nach Migration (Tray-Benachrichtigung; Klick öffnet die Seite)
### Telemetrie (EventIds App 1500–1519)
- [x] Log 1500 Migration ausgeführt (alter Wert, neuer Wert), 1501 Modus geändert, 1502 Hinweis gezeigt
- [x] Messwert `zoomin.tracking.mouse_push` (Frames „Ansicht geschoben“, Rate je Sekunde im
      Backend, Tag `zoomin.mouse.follow`) mit Instrument und Tags, die beim Start angelegt werden
- [x] Katalog `docs/kb/telemetrie.md`
### Doku
- [x] `docs/kb/eingabe-und-tracking.md`: neuer Abschnitt „Randabstand“, ADR-0018 bleibt gültig
      (nur nicht mehr Werkswert)

## Parallelisierung
- **Exklusiv:** `AnchorController.cs`, `TrackingOptions.cs`, Seite „Fokus & Verfolgung“.
- **Hotspots, nur ergänzend:** `AppSettings.cs`, `ProfileFields.cs`, `TrackingMapping.cs`,
  `Texts{German,English}*.cs`, `telemetrie.md`, `eingabe-und-tracking.md`.
- **Wartet auf:** nichts. Läuft parallel zu Schritt 0, B2, G1 und dem D1-Spike.
- **Merge:** als erste Spur in `feat/m17-ruhige-ansicht`. B2 nutzt den neuen Ankermodus für
  „hineinzoomen an der Rahmenposition“, darum sollte A1 vor B2 gemergt werden.

## Akzeptanzkriterien
- [ ] Maus innerhalb des Abstands bewegen → Bild steht still (Sichtprüfung per KI-Debug-Schnappschuss)
- [ ] Am Rand läuft das Bild gleichmäßig mit, ohne Springen; Zeiger bleibt auf dem Abstand
- [ ] Klicks treffen überall, auch am Monitorrand und in der Lupe
- [ ] Update von einer Version mit `CenterMouse`: einmal Randabstand + Hinweis, danach bleibt die Wahl
- [ ] Benchmark `0 B`, Bildrate unverändert
- [ ] Overlay weiterhin aus Aufnahmen ausgeschlossen

## Umsetzungsnotizen (2026-11-11)
- Grundsatz festgehalten in [ADR-0029](../../adr/0029-maus-randabstand-als-werkswert.md)
  (neuer Werkswert und einmalige Umstellung; ADR-0018 gilt weiter).
- Zoomwechsel im Randabstand-Modus: Der Zeiger bleibt auf dem Bildschirm stehen (Anker wird
  umgerechnet); ab 1× beginnt das Hineinzoomen am Zeiger. Im Plan nicht festgelegt, selbst
  entschieden – sonst rutscht der Inhalt unter dem Zeiger weg.
- Seitenname ist „Fokus & Textcursor“ (im Plan „Fokus & Verfolgung“).
- Migration speichert sofort; Profile aus 1.0 bekommen beim Laden den Werkswert Randabstand.
- Klick auf den Hinweis: neues `TrayCommand.OpenTracking`, `ITrayIcon.Notify(…, clicked)`.
  Windows meldet nur „Ballon geklickt“; der Klick gehört zum zuletzt gezeigten Hinweis. Hinweise
  laufen über den Tray-Thread (nach dem Anlegen des Symbols). Ein zweiter Hinweis ersetzt den
  ersten sofort (ein Ballon je Symbol, wie bisher): Beim Start mit dem Hinweis entfällt die
  UIAccess-Benachrichtigung dieses eine Mal (sie kommt bei jedem Start, Banner bleibt).
  `TrayNotices` hält Hinweise nur, solange das Symbol fehlt (höchstens 4, die neuesten) und zeigt
  sie dann sofort hintereinander. Ein „einer nach dem anderen“ (warten auf das Ende des Ballons)
  wurde verworfen: Es verzögerte Statushinweise (Filter pro Fenster), und Windows' Ende-Meldungen
  lassen sich keinem Hinweis sicher zuordnen. Texte werden auf 63/255 Zeichen gekürzt (sonst
  Ausnahme in der Fensterprozedur).
- Profile aus 1.0 werden nicht einzeln migriert: Sie haben kein `MouseFollow` und laden mit dem
  Werkswert Randabstand (passt zur Entscheidung „einmal auf Randabstand“). Auch die Migration
  stellt „Fixpunkt“ (`CenterMouse=false`) um – wie im Plan entschieden; im Review angemerkt.
- Restrisiko: Findet die Update-Prüfung kurz nach dem Start eine neue Version, ersetzt deren
  Hinweis den Randabstand-Hinweis; der Merker ist dann schon gelöscht.

## Offene Fragen / Risiken
- Wirkt 20 % bei Lupe (kleines Fenster) zu groß? Eventuell eigener Werkswert für die Lupe, im
  Sichttest prüfen.
- Die Migration stellt bewusst gewählte „Mitte“ um. Der Hinweis muss deutlich sein.
