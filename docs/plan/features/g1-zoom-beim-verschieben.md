# G1 · Zoom beim Fenster-Verschieben
**Meilenstein:** [M17](../m17-ruhige-ansicht.md) · **Spur:** C · **Branch:** `feat/m17-g1-fenster-verschieben` · **Größe:** S–M · **Status:** geplant

**Ziel:** Solange ein Fenster verschoben oder in der Größe geändert wird, geht der Zoom auf dem
Monitor, auf dem das Fenster gerade ist, vorübergehend auf 1×. Man sieht so sofort, wohin das
Fenster kommt. Nach dem Loslassen gleitet der Zoom zurück.
**Demo:** Vollbild 4×, Explorer-Fenster an der Titelleiste ziehen → Monitor gleitet auf 1× →
Fenster ablegen → Zoom gleitet auf 4× an der Mausposition. Auf den Zweitmonitor ziehen → dort 1×,
der erste Monitor bekommt seine Stufe zurück.
**Voraussetzungen:** Schritt 0 „vorübergehende Zoomstufe“ in `feat/m17-ruhige-ansicht`.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (G1, eigene Idee des Nutzers),
Bewertung: [feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Aktion | Werk **aufheben** (gleitet auf 1×, Filter bleiben); wählbar „absenken auf Stufe X“ (nur wenn höher) oder „unverändert“ |
| Rückkehr | sofort beim Loslassen, gleitend (normale Gleitzeit), an der Mausposition |
| Auslöser | zwei Schalter: „beim Verschieben“ (an) und „beim Größe ändern“ (an) |
| Modi | Vollbild und Lupe (Lupe blendet sich beim Ziehen aus); Angedockt unverändert |
| Monitor | der unter dem Zeiger; Wechsel während des Ziehens: neuer Monitor angepasst, alter bekommt seine Stufe zurück |
| Stufe | wird nicht gespeichert (vorübergehende Stufe aus Schritt 0) |
| Profil | alle Einstellungen gehören zum Profil |
| Kürzel | keins; Einschalten über die Einstellungen *(selbst entschieden)* |
| Absenk-Stufe | Werk 2× *(selbst entschieden)* |

## Umsetzung
- **Core**
  - `WindowDragPolicy` als reine Logik: Eingabe sind Ereignis (Start/Ende, Verschieben/Größe),
    Monitor unter dem Zeiger, aktuelle Stufe und Einstellungen. Ausgabe ist die vorübergehende
    Stufe je Monitor oder „nichts tun“.
  - Der Monitorwechsel beim Ziehen kommt aus der Zeigerposition (Tracking-Snapshot), nicht aus
    dem Fensterrechteck.
- **Platform**
  - `WindowEventHooks` hängt schon `EVENT_SYSTEM_MOVESIZESTART/END` auf dem Filter-pro-Fenster-Thread
    (out-of-context). Dazu kommt ein eigener schlanker Abonnent oder eine neue Quelle
    `WindowDragSource`. Sie meldet nur Start/Ende und Art, keinen Fenstertitel.
  - **Verschieben oder Größe:** `MOVESIZESTART` sagt das nicht direkt. Die Art ergibt sich aus der
    Trefferzone beim Start (`WM_NCHITTEST` per `SendMessageTimeout` an das Fenster: Titel =
    Verschieben, Rand = Größe) oder aus der ersten Rechteckänderung (Größe gleich =
    Verschieben). Das entscheidet der Mini-Spike in Aufgabe 1.
- **Rendering:** Die Lupe wird ausgeblendet, solange `hidden` im Snapshot steht. Sonst gibt es
  nichts Neues, das Gleiten macht `ZoomAnimator`.
- **App**
  - Karte „Beim Fenster-Verschieben“ auf „Zoom & Modi“: Aktion (Auswahl), Stufe (nur bei
    absenken), zwei Schalter.
  - Verdrahtung Quelle → `WindowDragPolicy` → `ZoomModel`.

## Aufgaben
### 1 · Mini-Spike (1 Tag)
- [ ] Verschieben vs. Größe sicher erkennen (Trefferzone vs. erste Rechteckänderung), auch bei
      Verschieben per Tastatur (Alt+Leertaste → Verschieben)
- [ ] App-Matrix: Explorer, Edge/Chrome (Fenster und Tab herausziehen), Teams, Office, Terminal,
      VS Code, Einstellungen-App (UWP). Ergebnis in `docs/kb/eingabe-und-tracking.md`
### 2 · Core
- [ ] `WindowDragPolicy` + Tests: alle drei Aktionen, Absenken nur wenn höher, Monitorwechsel beim
      Ziehen, Ende ohne Start, doppelter Start, Lupe/Angedockt, Zoom aus
- [ ] Abdeckung ≥ 90 %
### 3 · Platform (EventIds 2900–2919)
- [ ] `WindowDragSource` (Start/Ende/Art), Log 2900 Start (Prozessname, Fensterklasse, Art),
      2901 Ende, 2902 Art unklar
### 4 · App (EventIds 1540–1559)
- [ ] Settings-Felder, `ProfileFields`, `ZoomMapping`; Karte auf „Zoom & Modi“, Texte DE/EN,
      Bildschirmleser
- [ ] Ablauf `zoomin.view.window_drag` (Start bis Rückkehr; Tags Art, Aktion, Stufe vorher/nachher,
      Monitorwechsel ja/nein); keine Titel
- [ ] Katalog `telemetrie.md`

## Parallelisierung
- **Exklusiv:** `WindowDragPolicy`, `WindowDragSource`, Karte auf „Zoom & Modi“.
- **Hotspots, nur ergänzend:** `WindowEventHooks.cs` (nur neuen Abonnenten anhängen; Datei gehört
  sonst zu Filter pro Fenster), `AppSettings.cs`, `ProfileFields.cs`, `ZoomMapping.cs`,
  `Texts*.cs`, `telemetrie.md`, `eingabe-und-tracking.md`.
- **Wartet auf:** Schritt 0. Der Mini-Spike (Aufgabe 1) darf sofort parallel zu Schritt 0 starten.
- **Parallel zu:** A1, B2, D1-Spike.
- **Merge:** nach A1. Die Reihenfolge zu B2 ist egal.

## Akzeptanzkriterien
- [ ] Ziehen an der Titelleiste: 1× während des Ziehens, danach zurück an der Mausposition (KI-Debug)
- [ ] Größe ändern am Rand: gleiches Verhalten, abschaltbar
- [ ] Ziehen über zwei Monitore: nur der aktuelle Monitor ist auf 1×
- [ ] Gespeicherte Stufe unverändert nach dem Ziehen
- [ ] App-Matrix dokumentiert; Apps ohne Meldung sind als bekannte Einschränkung benannt
- [ ] Overlay weiterhin aus Aufnahmen ausgeschlossen

## Offene Fragen / Risiken
- Apps mit eigener Ziehlogik (Browser-Tabs, eigene Titelleisten) melden oft kein
  `MOVESIZESTART`. Plan B (Fensterposition bei gedrücktem Mausknopf beobachten) nur nach
  Rücksprache.
- Aero-Snap (Fenster an den Rand ziehen) zeigt seine Vorschau erst bei 1×. Das ist gewollt, im
  Sichttest prüfen.
- Win+Pfeil löst nichts aus, weil nicht gezogen wird. Das ist gewollt.
