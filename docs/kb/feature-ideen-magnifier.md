# Feature-Ideen für die Vergrößerung (Recherche 2026-11-11)

> **Clean-Room:** Nur öffentliche Quellen (Handbücher, Hilfeseiten, Produktseiten, Foren), in
> eigenen Worten. Fremde Funktionsnamen stehen nur als Bezug in Anführungszeichen. Umgesetzt wird
> aus dieser Beschreibung, nicht aus den Vorbildern (AGENTS.md §3).
> **Status:** gesammelt und bewertet, **nichts davon ist geplant**. Wer eines davon umsetzt,
> grillt es zuerst (eigener Meilenstein) und trägt hier den Link ein.

## Ausgangslage
ZoomIN hat schon: Vollbild 1–16× stufenlos, rechteckige Lupe, Angedockt (AppBar), Filter
(invertiert, Farbton erhalten, Graustufen, drei Zweifarb-Schemata, Kontrast/Helligkeit), Filter
pro Fenster, intelligentes Invertieren, Glättung (Catmull-Rom/bilinear/Pixel), großer farbiger
Zeiger mit Rand, anpassender Zeiger (M15, Branch), Suchhilfe (Kürzel/Schütteln/Ruhe/Strg),
Fadenkreuz, Fokusrahmen, Caret-Balken, Verfolgung von Maus/Fokus/Caret, Maus in der Mitte
(ADR-0018) oder Fixpunkt (proportional, ADR-0003), Zoom pro Monitor, Profile, sichere Desktops.
Schon gesammelt („Nach dem MVP“): Sprachausgabe, Line View, Auto-Lupe am Fokus, 64×, Input-Transform.

**Technische Leitplanken**, die viele Ideen berühren:
- Das Overlay ist durchklickbar. Klicks landen am echten Desktop-Punkt. Wo ZoomIN den Zeiger
  zeichnet, muss dieser Punkt angezeigt sein (ADR-0003/0018). Jede Ansicht, die nicht an der Maus
  hängt (eingefroren, geschwenkt, gesperrt), ist darum **reine Anzeige** oder braucht eine Regel,
  wohin der Klick geht.
- Der Mauszeiger wird bisher **nie versetzt** (M6-Entscheidung). Ideen mit `SetCursorPos` brauchen
  dafür eine Produktentscheidung.
- Ein D3D-Device pro Monitor (ADR-0010): Bild von Monitor A auf Monitor B braucht geteilte Texturen.
- Jedes Overlay bleibt aus Aufnahmen ausgenommen (§2.4). Eine Ausnahme bräuchte ein ADR.

Größen wie in der Roadmap: **S** ≈ wenige Tage · **M** ≈ 1–2 Wochen · **L** ≈ 2–4 Wochen.

## Empfehlung – Top 5
| # | Funktion | Warum zuerst | Größe |
|---|---|---|---|
| 1 | **Ruhige Mausverfolgung mit Randabstand** (A1) | Häufigste Klage über Vollbild-Lupen ist Schwindel/Übelkeit durch ständig wanderndes Bild. Reine Core-Logik im bestehenden `AnchorController`, voll testbar, kein Risiko für Klicks. | S–M |
| 2 | **Übersicht auf Knopfdruck** (B2) | Orientierung ist bei hoher Stufe das größte Problem („wo bin ich?“). Auf 1× wechseln, Rahmen zeigt den Ausschnitt, loslassen springt hin. Nutzt vorhandene Gleit-Animation und Rahmen-Shader. | S–M |
| 3 | **Eigene Farbschemata** (C1–C3) | Zwei frei wählbare Farben, Einfärben, Farbe ersetzen und Schemata für Farbsehschwächen. Jedes Vorbild hat das, ZoomIN nur feste Paare. Passt in den bestehenden Farbmatrix-Durchgang. | S–M |
| 4 | **Eingefrorene Ausschnitte** (B1) | Zwei entfernte Stellen gleichzeitig sehen (Uhr, Statuszeile, Chat, Formelzeile). Hoher Alltagsnutzen. Rendering ist einfach (zweites Rechteck aus demselben Capture). Die Bedienung ist die eigentliche Arbeit. | M |
| 5 | **Schwenken per Tastatur** (A3) | Lesen und Absuchen ohne Maus, mit Tempostufen, Sprung an Ränder/Mitte, Ansicht merken. Kernfunktion für Tastaturnutzer, gleiche Sprache wie die Verfolgung. Klärung nötig: Zeiger bleibt, wo er ist. | M |

**Knapp dahinter:** Text schärfen/fetter (D1, hoher Nutzen, aber Qualitätsrisiko mit Filtern),
Tooltips/Meldungen verfolgen (A4), Maus-Puffer und Achsensperre (A6, sehr klein),
Zeigersuche „Sonar“ und Anzeige-Bedingungen (E1, klein).

## Vorgemerkt (Nutzerwunsch 2026-11-11)
Diese Funktionen stehen auf der Liste möglicher neuer Features. Geplant ist noch keine. Vor der
Umsetzung kommt ein Grilling und ein eigener Meilenstein.

**Block 1 – Kern, in dieser Reihenfolge**
1. Ruhige Mausverfolgung mit Randabstand (A1)
2. Übersicht auf Knopfdruck (B2)
3. Eigene Farbschemata (C1–C3)
4. Schärfen und Fetter (D1) – erst nach einem Spike (Halos, Invertieren, GPU-Budget Onboard)
5. Eingefrorene Ausschnitte (B1)

Vorschlag: 1–3 als gemeinsamer Meilenstein (etwa 2–3 Wochen).

**Block 2 – kleine Extras, nebenbei**
- Caret-Formen, z. B. Keile über und unter dem Textcursor (E2)
- Anzeige-Bedingung für alle Markierungen: immer, nur in Ruhe, nur in Bewegung, nur bei gehaltener Taste (E1)
- Maus-Puffer am Monitorrand und Achsensperre mit Umschalt (A6)

**Dazu: Zoom beim Fenster-Verschieben (G1)** – siehe Abschnitt G.

## A · Verfolgung und Bewegung
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| A1 | **Randabstand für die Maus** („schieben“): Ansicht bewegt sich erst, wenn der Zeiger einen einstellbaren Rand (0–50 %) erreicht; dazu Wahl zentriert / proportional / schieben getrennt für Maus, Fokus, Caret | weniger Bildbewegung → weniger Schwindel; ruhigeres Lesen | S–M | Core: neuer Anker-Modus neben `CenterMouse`; Zeiger zeichnet ZoomIN ohnehin selbst, Klicks treffen weiter. Benchmark nötig (Hot Path). | ZoomText-Handbuch S. 146–148¹; GNOME² ; Orca³ |
| A2 | **Echte Mitte am Rand**: Objekt bleibt auch am Monitorrand mittig, außerhalb des Desktops Füllfarbe | Nutzer mit Gesichtsfeldausfall suchen nie am Rand | S–M | Shader zeichnet Füllfarbe statt Klemmen. Risiko: NVDA hat so einen Modus wieder entfernt (Fehler im Design) – erst Spike | ZoomText S. 148¹; Dolphin⁴; NVDA⁵ ⚠️ |
| A3 | **Schwenken per Tastatur** mit Tempostufen (gleiche Taste schneller, Gegenrichtung langsamer), Sprung an Ränder/Mitte, Ansicht speichern/zurückholen | Absuchen und Lesen ohne Maus | M | Anker vom Zeiger lösen; der gezeichnete Zeiger kann dann außerhalb liegen. Entscheidung: Zeiger mitnehmen (`SetCursorPos`) oder nicht | ZoomText S. 75–76¹; Dolphin-Lektion⁶ |
| A4 | **Tooltips, Warnmeldungen, neu aktive Fenster verfolgen**; Hinweis (Blinken am Rand), wenn eine Benachrichtigung außerhalb der Ansicht erscheint | Meldungen werden sonst übersehen | M | UIA-Ereignisse (`ToolTipOpened`, `WindowOpened`) bzw. WinEvents; keine Inhalte loggen. Risiko: Fehlauslöser durch Popups | ZoomText S. 144–145¹; macOS⁷; SuperNova 25.01⁸ |
| A5 | **Fokus-Verfolgung feiner**: immer / nur beim Tippen / nie; „nur so weit wie nötig“ vs. zentrieren; Tempo-Regler; kleine Sprünge gleiten, große springen; Steuerelement samt Elternmenü/-dialog einpassen | weniger unnötige Schwenks; Menü bleibt im Bild | S–M | Core (`AnchorController`, Fokus-Pan). Elternfenster per UIA-Baum, Rechtecke reichen | macOS⁷; ZoomText S. 147, 151–152¹ |
| A6 | **Maus-Puffer** am Monitorrand (Widerstand), **Achsensperre** mit Umschalt (nur waagrecht/senkrecht), Zeiger im aktiven Fenster halten | Zeiger rutscht nicht auf den anderen Monitor; Tabellenzeilen lesen | S | `ClipCursor` (kein Versetzen nötig). Freigabe zuverlässig bei Fokuswechsel/Sperre | Dolphin-Lektion⁹; ZoomText S. 150¹ |
| A7 | **Zeiger in die Ansicht holen** / Zeiger folgt Fokus; Kürzel „Zeiger auf nächsten Monitor“ | Zeiger nach Tastaturarbeit sofort wiederfinden | S | `SetCursorPos` → widerspricht „Zeiger nie versetzen“ (M6). Nur auf ausdrücklichen Befehl vertretbar | ZoomText S. 150¹; Orca³; Dolphin⁹ |
| A8 | **Ansicht folgt vorgelesenem Wort** | Mitlesen beim Vorlesen | M | erst mit `ZoomIN.Speech` | ChromeOS¹⁰; Windows-Lupe¹¹ |

## B · Ansichten
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| B1 | **Eingefrorene Ausschnitte**: Bereich aufziehen, bleibt als vergrößertes Fenster über dem Zoom (mehrere, verschieb-/größenbar, eigene Stufe, alle per Kürzel ein/aus) | zwei Stellen gleichzeitig im Blick | M | Zweites Quellrechteck aus demselben Capture, Rahmen-Shader vorhanden. Bereich verdeckt Desktop: reine Anzeige, Klick fällt durch. Aufziehen braucht einen Bearbeitungsmodus mit Maus-Hook. Bricht bei Layout-Änderungen (bei den Vorbildern auch) | ZoomText S. 94–95¹; Dolphin¹² |
| B2 | **Übersicht**: Kürzel → 1× mit Rahmen um den bisherigen Ausschnitt; Rahmen bewegen, loslassen = dort zoomen. Optional Rahmen dauerhaft im 1×-Teil bei Lupe/Angedockt | Orientierung bei hoher Stufe | S–M | Vorhandene Gleitanimation + Rahmen; Klick-Auswahl über Halten/Loslassen des Kürzels, kein Klick abfangen nötig | ZoomText S. 101–102¹; Dolphin-Lektion⁶ |
| B3 | **Freies festes Fenster**: Lupe, die an einer Stelle bleibt (nicht am Rand angedockt, keine AppBar) | eigenes Layout, z. B. neben dem Dokument | S | `ModeGeometry` um festes Zielrechteck; reine Anzeige wie Angedockt | ZoomText S. 82¹; macOS¹³ |
| B4 | **Zeilenstreifen**: volle Breite, wandert mit Zeiger/Caret auf und ab; Höhe passt sich der Texthöhe an | wie ein Leselineal | M | Lupe mit Breite 100 % + Caret-Höhe aus der Caret-Kette. Verdeckt Zeilen darüber/darunter | ZoomText S. 82¹ |
| B5 | **Lupe passt sich dem Fokus an** (Größe = fokussiertes Element) | Formularfelder ganz sehen | S–M | Gehört zur gesammelten „Auto-Lupe am Fokus“ – dort zusammenführen | Dolphin-Lektion⁶ |
| B6 | **Kurz 1× ansehen**: Kürzel halten = vorübergehend 1× (oder ganzes Bild), loslassen = zurück | schneller Überblick ohne Zoom zu verlieren | S | Strg+Alt+Z schaltet schon aus (Filter bleiben). Neu wäre nur „halten“. Mit B2 bündeln | macOS⁷; Windows-Lupe¹⁴ |
| B7 | **Zoomstufen-Politik**: Lieblingsstufen, nur ganze Zahlen, Min/Max für Mausrad | schneller zur gewohnten Stufe | S | `ZoomLimits`; heute ×1,25 multiplikativ | ZoomText S. 2¹; Dolphin 25.02¹⁵ |

## C · Farbe
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| C1 | **Zwei frei wählbare Farben** (Hell→Farbe 1, Dunkel→Farbe 2) und **Einfärben** in einem Farbton bei erhaltener Helligkeit | eigenes, angenehmes Kontrastpaar | S | Leuchtdichte → Verlauf zwischen zwei Farben ist eine affine Matrix; passt in `ColorMatrix` | ZoomText S. 117–120¹ |
| C2 | **Farbe ersetzen** (eine Farbe → andere, mit Toleranz) | schlecht sichtbare Akzentfarben retten | S–M | eigener Shader-Schritt (Farbabstand), nicht als Matrix. Kosten pro Pixel messen | ZoomText S. 119¹; Dolphin¹² |
| C3 | **Schemata für Farbsehschwächen** (Protan/Deutan/Tritan) und **Helligkeit/Kontrast je Kanal** | auch für Nutzer mit Farbsehschwäche | S | Matrizen aus der Fachliteratur, eigene Herleitung | Dolphin-Lektion¹⁶; GNOME² |
| C4 | **Filter pro Monitor** | heller Zweitmonitor, invertierter Hauptmonitor | M | `DisplayEffects` pro Pipeline ist da; Einstellungen/UI fehlen | Dolphin⁹ |
| C5 | Vergleich: Windows 11 testet ein **Kontrast-Design, das Fotos/Videos bunt lässt** | Messlatte für M13 | – | nur beobachten | pureinfotech¹⁷ ⚠️ Beta |

## D · Text
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| D1 | **Schärfen** (Kontrast/Radius) und **Fetter** (dunkle Striche verbreitern, Stufen) | dünne Schrift bei hoher Stufe lesbar | M | Shader nach der Skalierung (Unschärfemaske / Min-Filter). Risiko: Halos, Wechselwirkung mit Invertieren, GPU-Budget Onboard | Dolphin-Lektion⁶ |
| D2 | **Text neu zeichnen** statt vergrößern (gestochen scharf) | schärfste Darstellung | L | braucht Text-Abfang in fremden Prozessen (Hooks/Injection) → **nicht machbar** im Stack, verworfen | Dolphin-Lektion⁶; ZoomText S. 2¹ |
| D3 | **Textleiste**: Zeile unter Zeiger/Caret groß in eigener Schrift und Farbe am Rand; Text unter dem Zeiger bei gehaltener Taste groß anzeigen; Eingabefeld beim Tippen groß | Lesen ohne Zoomen | M–L | UIA `TextPattern` + DirectWrite im Overlay. Gehört zu „Line View“ – dort bündeln. Inhalte nie loggen | ZoomText S. 134–135¹; macOS Hover Text¹⁸ |
| D4 | **Lesefenster mit Umbruch** und Satz-Markierung | langes Lesen | L | erst mit Sprachausgabe | Dolphin¹⁹ |

## E · Zeiger, Caret, Fokus
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| E1 | **Suchhilfe-Formen** (Kreis, nach innen laufende Ringe) und **Anzeige-Bedingung** für alle Markierungen: immer / nur in Ruhe / nur in Bewegung / nur bei gehaltener Taste; Transparenz | Zeiger finden, ohne dass er stört | S | Zeiger-Durchgang vorhanden | ZoomText S. 121–132¹ |
| E2 | **Caret-Formen**: Keile über/unter dem Caret, Kreis, Rahmen; Wort/Zeile hervorheben | Caret beim Tippen sofort sehen (häufige Klage) | S | Caret-Rechteck vorhanden; Wort/Zeile über UIA-Textbereich (M) | ZoomText S. 127¹; Dolphin¹²; AppleVis²⁰ |
| E3 | **Fokus-Formen**: Fläche, Unterstrich, Rahmen mit Abstand; „kurz“ (1,5 s) statt dauerhaft; Wechsel je nach Maus/Tastatur | weniger Verdecken | S | Fokusrahmen vorhanden | ZoomText S. 131¹; Dolphin-Lektion¹⁶ |
| E4 | **Fadenkreuz**: Lücke um den Zeiger, Deckkraft, Länge | Inhalt am Zeiger bleibt sichtbar | S | Shader-Parameter | GNOME² |

## F · Mehrere Monitore und Sonstiges
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| F1 | **Zweiter Monitor zeigt den ersten** (1×-Übersicht oder Klon des vergrößerten Bildes, z. B. für Assistenz) | Überblick bzw. Helfer sieht mit | L | geteilte Texturen zwischen Devices (ADR-0010) oder zweites Capture. Klon für Helfer kollidiert nicht mit dem Aufnahme-Ausschluss | ZoomText S. 82–86¹; Dolphin⁹ |
| F2 | **Präsentationsmodus**: Vortragsmonitor vergrößert, Publikumsmonitor nie (auch keine Filter/Zeiger) | Vorträge halten | S | weitgehend da (Zoom pro Monitor); fehlt: Monitor fest ausschließen | Dolphin⁹ |
| F3 | **Ansicht eines Monitors sperren** (Ausschnitt bleibt stehen, während man auf dem anderen arbeitet) | Vorlage ansehen, auf dem anderen schreiben | M | Anker einfrieren; Klicks dort nur korrekt bei 1× – reine Anzeige | Windows-Beta¹⁷ ⚠️ |
| F4 | **Einstellungen pro App automatisch** (Profil folgt Vordergrund-App) | Excel anders als Browser | M | Prozessname des Vordergrundfensters (datenschutzkonform); baut auf Profilen auf | ZoomText S. 3¹ |
| F5 | **Kamera als Lupe** (Webcam/Dokumentenkamera: vergrößern, drehen, einfrieren, Filter) | Papier lesen | L | Media Foundation; eigenes Feature, nicht im Overlay-Kern | Dolphin²¹; ZoomText S. 3¹ |
| F6 | **Zoom bewusst in Übertragung zeigen** (Opt-in, für Schulungen) | Trainer zeigt, was der Nutzer sieht | S | technisch trivial, **verstößt gegen §2.4** → nur mit ADR | macOS⁷ |
| F7 | **Touch-Gesten** (Dreifingertipp, Aufziehen zum Zoomen) | Tablets/Convertibles | M–L | globale Gesten ohne Magnification-API schwierig | Dolphin-Lektion⁶ |
| F8 | Fernbildschirm/Tafel (Lehrer-PC) | Schule | L | außerhalb des Kerns, verworfen | Dolphin²² |

## G · Fenster verschieben (eigene Idee, Nutzerwunsch 2026-11-11)
| ID | Funktion | Nutzen | Größe | Machbarkeit / Risiko | Quelle |
|---|---|---|---|---|---|
| G1 | **Zoom beim Fenster-Verschieben**: Solange ein Fenster verschoben wird, ändert sich der Zoom auf dem Monitor, auf dem das Fenster gerade ist. Einstellung: *aufheben* (gleitet auf 1×, Filter bleiben), *absenken* auf eine wählbare Stufe, oder *unverändert*. | Beim Ziehen sieht man sofort, wohin das Fenster kommt, statt mit hoher Stufe blind zu schwenken | S–M | Siehe unten | eigene Idee; in den Vorbildern nicht gefunden |

**Umsetzung grob:**
- **Erkennen:** `SetWinEventHook` mit `EVENT_SYSTEM_MOVESIZESTART`/`…END`. Das läuft
  out-of-context, also ohne Code in fremden Prozessen, und gilt auch für Verschieben per Tastatur
  über das Fenstermenü.
- **Zoom ändern:** Der Monitor ist der unter dem Zeiger. Wechselt er beim Ziehen, wird der neue
  Monitor angepasst und der alte bekommt seine Stufe zurück. Das Gleiten (150 ms) kommt aus dem
  `ZoomModel`. Die Stufe des Nutzers wird nur vorübergehend überschrieben, nicht gespeichert.
- **Klicks:** Durch den Cursor-Fixpunkt bleibt das gezogene Fenster unter dem Zeiger, auch während
  sich der Zoom ändert.
- **Telemetrie:** Ablauf `zoomin.tracking.window_move` mit Monitor, Stufe vorher/nachher und Dauer.
  Fenstertitel werden nicht erfasst.

**Offene Fragen fürs Grilling:**
- Was gilt ab Werk: aufheben, absenken oder unverändert?
- Kommt die alte Stufe nach dem Loslassen sofort zurück oder nach einer kurzen Pause?
- Gilt das auch für die Fenstergröße ändern (`MOVESIZESTART` meldet beides)?
- Was passiert beim Andocken per Win+Pfeil (kein Ziehen) und bei Lupe und Angedockt?
- Soll ein Kurzbefehl die Funktion an- und ausschalten?

**Risiken:** Manche Apps ziehen ihre Fenster selbst (eigene Titelleisten, z. B. Browser-Tabs) und
melden kein `MOVESIZESTART`. Das muss eine App-Matrix prüfen. Ein Rückfall über die
Fensterposition beim gedrückten Mausknopf ist denkbar, aber teurer.

## Stimmen von Nutzern (Auswahl)
- Vollbild-Lupe macht nach Minuten **übel/schwindlig**; Angedockt ohne Kürzel für Größe/Lage;
  Text ohne Glättung zackig → A1, B3. ²³
- Windows-Lupe zentriert: **Zeiger versetzt** zur Taskleiste bei ≥ 300 %, seit Jahren → ZoomIN
  zeichnet den Zeiger selbst, Testfall aufnehmen. ²⁴
- Windows-Lupe **friert** beim Start ein, Updates machen Lupenmodus schlechter → Robustheit bleibt
  ein Unterscheidungsmerkmal. ²⁵
- Angedockt oben: neue Fenster öffnen **hinter** dem Streifen → AppBar-Verhalten prüfen. ²⁶
- Kommerzielle Lupen zeigen nach Windows-Grafik-Updates **Bildreste**, brauchen Ausweich-Engines;
  ZoomText bricht auf manchen AMD-GPUs → WARP-/GPU-Matrix weiterführen. ²⁷ ²⁸
- macOS: Fokus-Verfolgung klappt nur in einem Browser, **Caret schwer zu finden** → E2, M6-Matrix. ²⁰
- Keine Ansicht passt allen → schneller Wechsel der Modi bleibt wichtig. ²⁹

## Quellen
1. ZoomText-Benutzerhandbuch (aktuell, ohne Versionsangabe): https://support.freedomscientific.com/content/documents/manuals/ZoomText/ZoomText_User_Guide_English_US.pdf
2. GNOME Shell Magnification: https://wiki.gnome.org/Projects/GnomeShell/Magnification
3. Orca-Hilfe Lupe: https://svnlliurexnet.gva.es/leia/mods-backports/trunk/gnome-orca/help/C/preferences_magnifier.page
4. Dolphin Produktfunktionen: https://yourdolphin.com/product/features?id=4&pid=4
5. NVDA 2026.2 Beta-Hinweise (nur Suchauszug): https://www.nvaccess.org/?p=33413
6. Dolphin-Lektion Vergrößerung: https://learn.yourdolphin.com/supernova-magnifier?lesson=magnification&hsLang=en
7. macOS Zoom, erweitert: https://support.apple.com/en-za/guide/mac-help/mh35715/27/mac/27
8. SuperNova 25.01: https://yourdolphin.com/product/version?id=646
9. Dolphin-Lektion Mehrere Monitore: https://learn.yourdolphin.com/supernova-magnifier?lesson=multiple-monitors&hsLang=en
10. ChromeOS Lupe: https://support.google.com/chromebook/answer/6320705
11. Windows-Lupe Lesen: https://support.microsoft.com/en-us/accessibility/windows/magnifier/how-to-use-magnifier-reading
12. Dolphin Funktionen: https://yourdolphin.com/product/features?id=1&pid=1 · Lektion Bereiche: https://learn.yourdolphin.com/supernova-magnifier?lesson=hooked-areas&hsLang=en
13. macOS Zoom-Einstellungen: https://support.apple.com/guide/mac-help/change-zoom-settings-for-accessibility-mh40579/mac
14. Erfahrungsbericht Windows-Lupe: https://veroniiiica.com/windows-magnifier-and-low-vision/
15. SuperNova 25.02: https://yourdolphin.com/news?id=564
16. Dolphin-Lektion Bildschirm-Hervorhebungen: https://learn.yourdolphin.com/supernova-magnifier?lesson=screen-enhancements&hsLang=en
17. Windows 11 Beta-Lupe/Kontrast: https://pureinfotech.com/?p=192700 · https://pureinfotech.com/?p=192020
18. macOS Hover Text: https://support.apple.com/guide/mac-help/mchl46661917/mac
19. siehe 4
20. AppleVis (nur Suchauszug): https://applevis.com/forum/low-vision-accessibility-apple-products/features-zooming-automatically-mac-os
21. Dolphin-Lektion Kameras: https://learn.yourdolphin.com/supernova-magnifier?lesson=cameras&hsLang=en
22. Dolphin-Lektion Tafel: https://learn.yourdolphin.com/supernova-magnifier?lesson=whiteboards&hsLang=en
23. siehe 14
24. Microsoft Q&A: https://learn.microsoft.com/en-us/answers/questions/5904684/win-11-magnifier-mouse-cursor-alignment-issues
25. Microsoft Tech Community: https://techcommunity.microsoft.com/discussions/windows11/windows-11-magnifier-broken/4523473
26. Tom's Hardware: https://forums.tomshardware.com/threads/does-anyone-have-problems-when-docking-the-magnifier-on-top-and-some-windows-dont-open-below-it.3780205/
27. Dolphin KB Bildreste: https://kb.yourdolphin.com/knowledge/screen-artifacts-in-supernova-24
28. ZoomText Legacy-Engine: https://sastltd.zohodesk.eu/portal/en/kb/articles/use-zoomtext-legacy-mag-engine-to-fix-video-issues
29. AppleVis (nur Suchauszug): https://applevis.com/forum/low-vision-accessibility-apple-products/setting-mac-mini-low-vision

**Nicht verifiziert:** ZoomText-Neuerungen 2026 (Seiten gesperrt), AccessWorld-Tests (nicht
gelesen), Windows-Beta-Funktionen (nur Fachpresse), runde Lupen (in keiner aktuellen Doku gefunden).
