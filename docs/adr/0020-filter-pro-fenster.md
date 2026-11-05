# ADR-0020: Farbfilter pro Fenster mit automatischer Erkennung heller Fenster

- **Status:** Angenommen
- **Datum:** 2026-11-05
- **Nummer:** 0018 liegt auf `feat/tracking-compat`, 0019 auf `docs/m11-telemetrie-plan` (beide noch
  nicht in `develop`).

## Kontext
Der Farbfilter ist heute **eine Farbmatrix pro Monitor** (`MonitorFilter`: an/aus und Filterart je
Monitor, Kontrast/Helligkeit gemeinsam). `Magnify.hlsl` wendet sie auf jedes Pixel gleich an.
Nutzer, die invertieren, haben ein Problem mit gemischten Bildschirmen: Eine helle App (Word,
Excel, viele Webseiten) blendet, eine dunkle App wird durch „Invertiert“ dagegen hell. Gewünscht:
Fenster, die hell sind, werden automatisch invertiert – standardmäßig, pro Fenster, korrigierbar.

## Entscheidung
1. **Pro Fenster.** Der Filter wirkt nur auf die sichtbare Fläche eines Fensters. ZoomIN führt die
   sichtbaren Rechtecke der Fenster (Z-Reihenfolge, Verdeckung) und gibt dem Shader eine begrenzte
   Zahl Bereiche mit je einer Filtermatrix. Mehr Bereiche als das Limit: Verhalten im Spike festlegen.
2. **Einstufung einmal beim Öffnen.** Jedes Fenster wird – hell **und** dunkel – nur beim Öffnen
   eingestuft: 3 Messungen der Helligkeit über ca. 1 s auf der GPU (verkleinertes Bild), 2 von 3
   müssen übereinstimmen, sonst bis zu 2 s weiter. Bis dahin bleibt es ungefiltert. Danach misst
   ZoomIN bis zum Schließen nicht mehr (auch nicht bei Fokus).
3. **Schwelle:** hell, wenn ≥ 60 % der Fläche sehr hell sind (Leuchtdichte ≥ 80 %). Regler
   „Empfindlichkeit“ (niedrig/mittel/hoch), unter „Erweitert“ der genaue Prozentwert.
4. **Nutzerwahl hat Vorrang** und wird an **App + Fensterart** gemerkt (Programmname +
   Fensterklasse, nie der Fenstertitel). Danach misst die Automatik für diese Fensterart nicht mehr.
   Wahl pro Eintrag: „Automatisch“, „Keiner“ (nie filtern, auch nicht durch den Monitorfilter)
   oder jeder vorhandene Filter. Die Automatik nutzt standardmäßig „Invertiert“ (änderbar).
5. **Zusammenspiel mit dem Monitorfilter:** „Ganzen Monitor invertieren“ (`Strg+Alt+C`) bleibt der
   schnelle Standardweg. Ein Fenster, das schon für sich invertiert ist, wird dabei herausgerechnet
   – **nie doppelt invertiert**. Ein dunkles Fenster wird durch den invertierten Monitor hell.
   Reihenfolge je Fenster: Nutzerwahl → Automatik → Monitorfilter. Kontrast/Helligkeit gelten
   weiter für alles.
6. **Popups:** Menüs, Tooltips und Aufklapplisten erben den Filter des Besitzerfensters (keine
   Messung, kein Aufblitzen). Echte Dialoge werden gemessen und haben eine eigene Fensterart.
7. **Ausnahmen ab Werk:** Medien- und Bildbearbeitungs-Apps als „Keiner“ vorbelegt; die
   Windows-Oberfläche (Taskleiste, Startmenü, Desktop, Infobereich, Benachrichtigungen) wird nie
   gefiltert. Vollbild-Anwendungen sind **nicht** ausgenommen.
8. **Immer an, auch ohne Zoom.** Ist kein Fenster zu filtern, pausiert das Overlay ganz. Für die
   Prüfung neuer Fenster wird kurz aufgenommen, aber nicht gezeichnet.
9. **Bedienung:** Detailseite „Filter pro Fenster“ (von der Farbfilter-Seite aus) mit allen
   gemerkten Einträgen; Tastenkürzel „Filter für aktives Fenster umschalten“, ab Werk **nicht
   belegt** (Seite „Tastenkürzel“).
10. **Budget** (1×, ein invertiertes Fenster, 1440p, Onboard-Grafik): ≤ 3 % CPU, Bildrate =
    Monitor-Hz, ≤ 1 Frame Verzögerung, Filterbereich folgt beim Verschieben ohne sichtbaren
    Versatz; Render-Loop ohne Allokation.

## Verworfene Alternativen
- **Ganzer Monitor nach aktiver App:** einfach, invertiert aber Nachbarfenster mit.
- **Inhaltsbasiert pro Pixel („intelligentes Invertieren“):** kein Fensterbezug, verfälscht Fotos
  und Videos, Farben kippen an Kanten.
- **Laufend messen:** folgt Seitenwechseln im Browser, kostet aber dauerhaft GPU-Zeit für jedes
  Fenster und kann flackern.
- **Merken nur pro App:** ein heller Dialog in einer dunklen App folgt dann nicht der Automatik.
- **Merken an App + Fenstertitel:** speichert Dokument- und Seitennamen.

## Konsequenzen
- ✅ Helle Fenster blenden nicht mehr, dunkle bleiben dunkel; kein doppeltes Invertieren.
- ✅ Dunkel eingestufte Fenster kosten nach der ersten Sekunde praktisch nichts.
- ⚠️ Großer Umbau im Render-Pfad (Fensterbereiche pro Frame, Matrix je Bereich im Shader).
- ⚠️ Das Overlay läuft für jeden Nutzer, sobald ein helles Fenster sichtbar ist: 1 Frame
  Verzögerung im Desktop, Akku, mögliche Reaktion von Spielen/Anti-Cheat auf das Abgreifen.
- ⚠️ Einstufung nur beim Öffnen: Wird ein Fenster danach hell/dunkel (Browser-Seitenwechsel,
  Designwechsel) oder lädt es länger als 2 s, bleibt sie falsch, bis man sie korrigiert.
- ⚠️ Apps mit einer Fensterklasse für alles (Chromium/Electron: `Chrome_WidgetWin_1`) haben nur
  eine Regel für alle ihre Fenster.
- ⚠️ 1.0 verschiebt sich um diesen Meilenstein (M12 vor 1.0).
