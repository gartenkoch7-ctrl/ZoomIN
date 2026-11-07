# Freigabe-Filter (Teams-Bildschirmfreigabe)

Entscheidung und Begründung: [ADR-0018](../adr/0018-freigabe-filter-erkennung-ueber-fenstermerkmale.md).

## Ziel
Während der Nutzer in Teams teilt, sieht **nur er** den geteilten Bereich mit einem eigenen
Filter. Die Zuschauer sehen Originalfarben, weil das Overlay aus Aufnahmen ausgeschlossen ist
([capture-ausschluss.md](capture-ausschluss.md)).

## Bausteine
| Schicht | Typ | Aufgabe |
|---|---|---|
| Core | `Sharing/ShareDetector` | Rahmenfenster finden, Bildschirm- vs. Fensterfreigabe |
| Core | `Sharing/ShareModel`, `ShareState` | Erkennung + Kürzel → wirksamer Bereich und Matrix (atomarer Tausch) |
| Core | `Sharing/ShareGeometry.TryClip` | Bereich pro Monitor, pro Frame, ohne Allokation |
| Platform | `Sharing/TeamsShareSource` | Erkennung 2×/s, geteiltes/markiertes Fenster 30×/s nachführen |
| Platform | `Sharing/MeetingWindowReader` | Fenster der Teams-Prozesse lesen (nur Rechteck und Stile) |
| App | `ShareFactory`, `ShareFilterViewModel` | Einstellungen ↔ Modell, Seite „Farbfilter“ |

Regeln (Nutzerentscheidung 2026-11-03):
- Markierung per Kürzel hat Vorrang vor der Erkennung.
- Kürzel, während etwas angezeigt wird → aus; eine erkannte Freigabe bleibt aus, bis Teams sie
  beendet. Kürzel, während nichts angezeigt wird → Fenster (Strg+Alt+W) bzw. Monitor
  (Strg+Alt+Umschalt+W) unter der Maus markieren.
- Markiertes Fenster geschlossen oder minimiert → Markierung endet.
- Ein Fenster über zwei Monitore wird pro Monitor zugeschnitten.

## Erkennungsmerkmale (Annahme)
⚠️ unverifiziert, die Probe steht noch aus. Annahme aus dem sichtbaren Verhalten von Teams: Der
Rahmen um den geteilten Bereich ist ein eigenes Fenster des Teams-Prozesses, topmost, layered und
klick-durchlässig (`WS_EX_TRANSPARENT`). Bei Bildschirmfreigabe ist es so groß wie der Monitor,
bei Fensterfreigabe so groß wie das Fenster, und es folgt ihm.

Schwellwerte in `ShareDetector`: Rahmen ≥ 150 px pro Seite, Monitor-Toleranz 8 px.

## Probe
`./scripts/probe-meeting-windows.ps1 -Out <datei>`: einmal ohne Freigabe, dann bei Bildschirm-
und bei Fensterfreigabe. Das Skript gibt Klasse, Titel, Rechteck, Style/ExStyle, Topmost,
Cloaked, Display-Affinity und UIA-Namen aus. Nur lesen, nichts hooken.

| Datum | Teams-Version | Ergebnis |
|---|---|---|
| 2026-11-03 | – | Teams lief auf dem Entwicklungs-PC nicht; Probe offen |

Nach der Probe hier eintragen: Klassenname und ExStyle des Rahmens, ob er bei Fensterfreigabe
größer als das Fenster ist (Rahmenbreite), die Fenster der Freigabe-Leiste und ob Teams sich
selbst per `WDA_EXCLUDEFROMCAPTURE` ausblendet (Spalte `affinity`).

## Offen
- **Zeichnen im Overlay**: Shader-Bereich mit eigener Farbmatrix kommt mit der Arbeit
  „Benachrichtigungen vom Filter ausnehmen“ (`Magnify.hlsl`, `MagnifyConstants`). Danach:
  `ShareModel` an die Render-Threads geben, pro Monitor `ShareGeometry.TryClip` → Quell-UV-Rechteck,
  GPU-Test auf WARP, Benchmark 0 B. Bei aktivem Bereich braucht es das Overlay auch bei 1× ohne
  normalen Filter (wie `FilterSettings.ChangesColors`).
- Fenster, die über dem geteilten Fenster liegen, bekommen im überlappenden Teil ebenfalls den Filter.
