# ADR-0018: Freigabe-Filter – Teams-Freigabe an Fenstermerkmalen erkennen, Kürzel als Ersatz

- **Status:** Angenommen
- **Datum:** 2026-11-03

## Kontext
Wunsch: Während der Nutzer in Teams seinen Bildschirm oder ein Fenster teilt, soll er selbst den
geteilten Bereich mit einem eigenen Filter sehen (z. B. Gelb auf Schwarz). Außerhalb gilt sein
normaler Filter. Weil das Overlay `WDA_EXCLUDEFROMCAPTURE` trägt (ADR-0002), sehen die Zuschauer
weiter die Originalfarben. Das ist gewollt, die Zuschauer sollen den Filter nicht sehen.

Teams bietet keine öffentliche Schnittstelle, die meldet, ob und was gerade geteilt wird.
Sichtbar ist nur das Verhalten: Teams zeichnet während der Freigabe einen farbigen Rahmen um den
geteilten Bildschirm bzw. das geteilte Fenster und blendet eine Freigabe-Leiste ein.

## Entscheidung
- **Erkennung über Fenstermerkmale, nicht über Namen.** `TeamsShareSource` (Platform) liest etwa
  2×/s die sichtbaren Top-Level-Fenster der Teams-Prozesse (`ms-teams`, klassisch `Teams`):
  Rechteck, `WS_EX_TOPMOST`, `WS_EX_LAYERED` + `WS_EX_TRANSPARENT` (klick-durchlässig).
  `ShareDetector` (Core) hält das größte topmost-klick-durchlässige Fenster ab 150 px für den
  Rahmen. Liegt es bis auf 8 px auf einem Monitor, gilt das als Bildschirmfreigabe, sonst als
  Fensterfreigabe mit dem Rahmenrechteck. Klassennamen und Titel werden bewusst nicht verwendet,
  weil sie sich mit jedem Teams-Update ändern können.
- **Fenster folgen:** Wird ein Fenster geteilt, liest die Quelle nur dessen Rahmen 30×/s nach
  (ein Aufruf), damit der Filter beim Verschieben mitgeht.
- **Kürzel als Ersatz (Schema ADR-0008):** Strg+Alt+W markiert das Fenster unter der Maus (es
  folgt beim Verschieben, endet beim Schließen/Minimieren), Strg+Alt+Umschalt+W den Monitor
  unter der Maus. Zeigt schon etwas den Freigabe-Filter, blendet das Kürzel ihn aus. Eine
  erkannte Freigabe bleibt dann aus, bis Teams die Freigabe beendet. Eine Markierung per Kürzel
  hat Vorrang vor der Erkennung.
- **Nur beobachten:** keine Hooks, keine Injektion, kein Lesen von Fensterinhalten, nur
  Fensterrechtecke und Stile, wie jede Fensterverwaltung sie sieht (Clean-Room sinngemäß).
- **Filter:** eigener `ColorFilter` (Standard Gelb auf Schwarz), ersetzt im geteilten Bereich den
  normalen Filter; Kontrast und Helligkeit gelten dort nicht. In den Einstellungen unter
  Farbfilter → „Bei Bildschirmfreigabe“, gespeichert pro Profil.

## Konsequenzen
- Ändert Teams das Aussehen des Rahmens (z. B. kein eigenes Fenster mehr), fällt die Erkennung
  aus. Die Kürzel funktionieren dann weiter. Prüfen mit `scripts/probe-meeting-windows.ps1`, Werte
  in [freigabe-filter.md](../kb/freigabe-filter.md).
- Bei einer Fensterfreigabe gilt der Filter für das Rechteck des Fensters. Liegt ein anderes
  Fenster darüber, bekommt dessen überlappender Teil den Freigabe-Filter auch.
- Andere Meeting-Apps (Zoom, Webex, Browser-Teams) werden nicht erkannt; Kürzel nutzen.

## Verworfen
- **Graph-/Teams-API**: meldet keinen lokalen Freigabezustand, bräuchte zudem Anmeldung.
- **UI Automation der Teams-Oberfläche** („Freigabe beenden“-Knopf suchen): hängt an
  lokalisierten Texten und am WebView-Baum, bricht bei Updates und Sprachwechsel; bleibt höchstens
  ergänzende Bestätigung, falls die Probe zeigt, dass der Rahmen allein nicht eindeutig ist.
- **Hooks in Teams oder Abgreifen der Capture-Sitzung**: verstößt gegen „nur beobachten“ und
  wäre fragil.
