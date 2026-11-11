# Feature-Priorisierung: Wichtigkeit und Potenzial

> Bewertet werden die vorgemerkten Features aus [feature-ideen-magnifier.md](feature-ideen-magnifier.md).
> Dort stehen Beschreibung, Machbarkeit, Risiken und Quellen (IDs wie A1, B2).

## Priorisierung nach Wichtigkeit (2026-11-11)
Diese Reihenfolge gilt für alle vorgemerkten Features. Sie ersetzt die erste Top-5-Liste und die
Blöcke 1/2 vom selben Tag. Geplant ist noch keines. Vor der Umsetzung kommt ein Grilling, dann ein
Meilenstein.

**Bewertung:** Jedes Kriterium von 1 bis 5 Punkten, gewichtet, höchstens 35 Punkte.
- **Nutzen ×3:** wie stark es Sehbehinderten im Alltag hilft
- **Reichweite ×2:** wie viele Nutzer es betrifft und wie oft
- **Aufwand ×1:** 5 = klein
- **Risiko ×1:** 5 = gering; zählt Klicks, Hot Path, Grundsatzentscheidungen

Bei Punktgleichstand steht das Feature mit dem geringeren Risiko vorn.

| Rang | Feature | Nutzen | Reichw. | Aufwand | Risiko | Punkte | Begründung in einem Satz |
|---|---|---|---|---|---|---|---|
| 1 | Ruhige Mausverfolgung mit Randabstand (A1) | 5 | 5 | 4 | 4 | **33** | Trifft jeden Vollbild-Nutzer bei jeder Mausbewegung und nimmt die häufigste Klage, Schwindel. |
| 2 | Übersicht auf Knopfdruck (B2) | 4 | 4 | 4 | 4 | **28** | Orientierung bei hoher Stufe ist das zweitgrößte Alltagsproblem, und das Feature ist klein. |
| 3 | Schärfen und Fetter (D1) | 5 | 4 | 3 | 2 | **28** | Lesbarer Text ist der Kern einer Lupe; erst nach einem Spike, denn Halos und GPU-Budget sind offen. |
| 4 | Eigene Farbschemata (C1–C3) | 4 | 3 | 4 | 5 | **27** | Für Blendempfindliche und Farbsehschwache entscheidend, technisch fast ohne Risiko. |
| 5 | Caret-Formen (E2) | 3 | 4 | 5 | 5 | **27** | Den Caret beim Tippen nicht zu finden ist eine häufige Klage; ein Caret-Balken ist schon da. |
| 6 | Zoom beim Fenster-Verschieben (G1) | 4 | 4 | 4 | 3 | **27** | Fenster bei hoher Stufe zu ziehen ist heute ein Blindflug; Risiko: Apps ohne Verschiebe-Meldung. |
| 7 | Eingefrorene Ausschnitte (B1) | 4 | 3 | 3 | 3 | **24** | Großer Nutzen für Profis (Uhr, Chat, Formelzeile), aber Bedienung und Klicks sind aufwendig. |
| 8 | Anzeige-Bedingung für Markierungen (E1) | 2 | 3 | 5 | 5 | **22** | Komfort, damit Markierungen weniger stören; klein, nebenbei mitnehmen. |
| 9 | Maus-Puffer und Achsensperre (A6) | 3 | 2 | 5 | 4 | **22** | Hilft vor allem mit mehreren Monitoren und beim Lesen von Tabellen; sehr klein. |
| 10 | Einfrieren und Monitor wechseln (G2) | 4 | 2 | 3 | 2 | **21** | Stark für Nutzer mit mehreren Monitoren, braucht aber ein ADR (Zeiger versetzen) und eine Lösung für die Klicks. |

**Bündel für Meilensteine:**
- **„Ruhige Ansicht & Orientierung“ – A1, B2, G1 (M, ~2–3 Wochen).**
  - Alle drei ändern nur Anker und Stufe im `ZoomModel`.
  - B2 und G1 brauchen denselben Baustein „Stufe vorübergehend überschreiben, danach zurück“.
    Das Halten-Kürzel B6 kommt damit fast umsonst.
  - Kein Shader, keine Grundsatzfrage. Deshalb ist das der empfohlene nächste Meilenstein.
- **„Lesbarkeit“ – D1-Spike, C1–C3, E2, E1 (M–L).**
  - Alles läuft im Shader und im Zeiger-Durchgang.
  - Der D1-Spike entscheidet, ob Schärfen und Fetter dazukommen.
- **„Mehrere Ansichten“ – B1, G2, dazu A6 (L).**
  - Gemeinsames Problem: Ansichten, die nicht an der Maus hängen, und Klicks dorthin.
  - Davor braucht es ein ADR „Zeiger versetzen nur auf Befehl“ und eine Entscheidung zur Klickregel.
  - Teile von G2 können vorgezogen werden: der Monitorwechsel per Kürzel (S) und der Maus-Puffer
    aus A6 kommen ohne Einfrieren aus.

**Übrige Ideen, nicht vorgemerkt (grobe Stufe):**
- **Als Nächstes nachrücken:**
  - Tooltips und Meldungen verfolgen (A4)
  - feinere Fokus-Verfolgung (A5)
  - Einstellungen pro App (F4)
  - Präsentationsmodus (F2, klein)
  - Zoomstufen-Politik (B7, klein)
  - freies festes Fenster (B3)
  - Filter pro Monitor (C4)
- **Später, mit Grundsatzfrage:**
  - Schwenken per Tastatur (A3)
  - echte Mitte am Rand (A2, Spike)
  - Zeilenstreifen (B4)
  - zweiter Monitor zeigt den ersten (F1)
- **Mit der Sprachausgabe:** Textleiste (D3), Lesefenster (D4), Ansicht folgt dem Vorlesen (A8)
- **Nicht verfolgen:**
  - Text neu zeichnen (D2)
  - Kamera (F5)
  - Zoom in Übertragung zeigen (F6), außer mit ADR
  - Touch-Gesten (F7)
  - Tafel (F8)

## Potenzial (2026-11-11)
Die Rangliste oben fragt, was **jetzt am meisten hilft**. Das Potenzial fragt, was ein Feature
**für ZoomIN als Projekt** bringen kann.

**Bewertung:** Jedes Kriterium von 1 bis 5 Punkten, ungewichtet, höchstens 20 Punkte.
- **Alleinstellung:** Fehlt das der kostenlosen Windows-Lupe? Haben es nur kommerzielle Lupen
  oder gar keine?
- **Wirkung:** Gewinnt oder hält es Nutzer, die sonst aufgeben oder bei einer kommerziellen Lupe
  bleiben?
- **Ausbau:** Ist es ein Baustein, auf dem weitere Features aufsetzen?
- **Sichtbarkeit:** Merkt man es sofort, und lässt es sich vorführen (Video, Vergleich, Empfehlung)?

| Rang | Feature | Alleinst. | Wirkung | Ausbau | Sichtb. | Potenzial | Einschätzung |
|---|---|---|---|---|---|---|---|
| 1 | Ruhige Mausverfolgung (A1) | 2 | 5 | 3 | 2 | **12** | Windows und die kommerziellen Lupen können das schon. Ohne das Feature springen Nutzer aber ab (Schwindel). Grundausstattung, kein Aushängeschild. |
| 2 | Übersicht auf Knopfdruck (B2) | 2 | 4 | 4 | 4 | **14** | Windows hat nur eine kurze Vorschau ohne Rahmen. Der Baustein „Stufe vorübergehend ändern“ trägt G1 und das Halten-Kürzel. |
| 3 | Schärfen/Fetter (D1) | 4 | 5 | 3 | 5 | **17** | Höchstes Potenzial. Die Windows-Lupe hat nichts dergleichen, schärfere Schrift sieht man sofort, und für viele entscheidet die Schrift über den Kauf einer kommerziellen Lupe. |
| 4 | Eigene Farbschemata (C1–C3) | 3 | 3 | 3 | 4 | **13** | Windows hat Farbfilter für Farbsehschwächen, aber keine freien Farbpaare. Ausbau zu Schemata pro Monitor oder pro Fenster ist möglich. |
| 5 | Caret-Formen (E2) | 1 | 3 | 2 | 3 | **9** | Windows hat seit 2020 eine eigene Caret-Markierung. Ein Mehrwert entsteht nur, wenn die Markierung mit dem Zoom wächst und zu den Filtern passt. |
| 6 | Zoom beim Fenster-Verschieben (G1) | 5 | 4 | 3 | 4 | **16** | In keiner der untersuchten Lupen gefunden, also ein echtes Alleinstellungsmerkmal. Lässt sich in einem kurzen Video gut zeigen. |
| 7 | Eingefrorene Ausschnitte (B1) | 4 | 4 | 4 | 4 | **16** | Bisher nur in teuren Lupen. Grundlage für G2 und das Sperren einer Monitor-Ansicht. |
| 8 | Anzeige-Bedingungen (E1) | 2 | 2 | 2 | 1 | **7** | Feinschliff für Fortgeschrittene, kaum sichtbar. |
| 9 | Maus-Puffer/Achsensperre (A6) | 3 | 2 | 2 | 1 | **8** | Nützlich im Alltag, aber unauffällig. Nur der Maus-Puffer betrifft mehrere Monitore. |
| 10 | Einfrieren + Monitor wechseln (G2) | 3 | 4 | 4 | 4 | **15** | Stark für Arbeitsplätze mit mehreren Monitoren. Windows testet eine gesperrte Ansicht im Beta-Kanal, der Vorsprung kann also schrumpfen. |

**Quellen zu Windows:**
- Textcursor-Anzeige seit Windows 10 2004 (Seite „Textcursor“ in den Bedienungshilfen)
- Farbfilter für Farbsehschwächen seit Windows 10 1709
- Beta-Funktionen laut [feature-ideen-magnifier.md](feature-ideen-magnifier.md) (Quelle 17) ⚠️

## Gesamtbild: Priorität × Potenzial
| Feature | Priorität (von 35) | Potenzial (von 20) | Einordnung | Empfehlung |
|---|---|---|---|---|
| Ruhige Mausverfolgung (A1) | 33 | 12 | **Pflicht** | Zuerst bauen; ohne es bleiben Nutzer nicht |
| Übersicht auf Knopfdruck (B2) | 28 | 14 | **Jetzt** | Mit A1 und G1 im nächsten Meilenstein |
| Zoom beim Fenster-Verschieben (G1) | 27 | 16 | **Jetzt** | Im nächsten Meilenstein; Aushängeschild |
| Schärfen/Fetter (D1) | 28 | 17 | **Jetzt** | Machbarkeitstest gleich parallel zum nächsten Meilenstein; bei Erfolg Kern von „Lesbarkeit“ |
| Eigene Farbschemata (C1–C3) | 27 | 13 | **Jetzt** | Meilenstein „Lesbarkeit“ |
| Eingefrorene Ausschnitte (B1) | 24 | 16 | **Strategisch** | Nach einer Grundsatzentscheidung zur Klickregel; großer Hebel |
| Einfrieren + Monitor wechseln (G2) | 21 | 15 | **Strategisch** | Mit B1 zusammen; Monitorwechsel per Kürzel vorziehbar |
| Caret-Formen (E2) | 27 | 9 | **Schneller Gewinn** | Nur die Varianten, die Windows nicht hat (mit Zoom wachsend, Keile); klein halten |
| Maus-Puffer/Achsensperre (A6) | 22 | 8 | **Nebenbei** | Wenn gerade jemand am Zeigercode arbeitet |
| Anzeige-Bedingungen (E1) | 22 | 7 | **Nebenbei** | Zuletzt, oder nur auf Nachfrage von Nutzern |

**Einordnungen:**
- **Pflicht:** hohe Priorität, wenig Alleinstellung
- **Jetzt:** beides hoch
- **Strategisch:** hohes Potenzial, aber aufwendig oder mit Grundsatzfrage
- **Schneller Gewinn:** klein, nötig, wenig Potenzial
- **Nebenbei:** beides niedrig

**Folgerung:**
- Die Empfehlung bleibt: Der nächste Meilenstein ist „Ruhige Ansicht & Orientierung“ (A1, B2, G1).
- Neu ist, den Machbarkeitstest für Schärfen/Fetter (D1) **gleich parallel** zu starten. D1 hat
  das höchste Potenzial, und vom Test hängt der übernächste Meilenstein ab.
- Die Caret-Formen (E2) rutschen trotz hoher Priorität nach hinten, weil Windows das schon kann.
