# ADR-0021: Filter pro Fenster – Umsetzung (Ergänzung zu ADR-0020)

- **Status:** Angenommen, Nr. 1 und 2 teilweise ersetzt durch [ADR-0022](0022-filter-pro-fenster-nachbesserung.md)
- **Datum:** 2026-11-06
- **Ergänzt:** [ADR-0020](0020-filter-pro-fenster.md) (bleibt gültig; hier stehen die Punkte, die der
  Spike festgelegt oder anders gelöst hat)

## Kontext
ADR-0020 ließ offen, wie viele Bereiche der Shader nimmt, was bei mehr passiert und wie gemessen
wird; es nannte eine Messung „auf der GPU“. Beim Umsetzen von M12 zeigte sich, dass eine GPU-Messung
den schlafenden Render-Thread wecken und Desktop Duplication starten müsste – genau das, was die
Pause (ADR-0020 Nr. 8) vermeiden soll.

## Entscheidung
1. **Messung mit GDI statt GPU.** Ein Blick kopiert die Fensterfläche per `StretchBlt(HALFTONE)` in
   ein 64×64-Bitmap auf dem Tracker-Thread. Die Overlays sind per `WDA_EXCLUDEFROMCAPTURE` aus der
   Kopie ausgenommen, der Blick sieht also den Originaldesktop. Gezählt werden nur Pixel, die
   wirklich das Fenster zeigen (nicht von Fenstern darüber verdeckt, auf einem Monitor). Unter 15 %
   sichtbar → der Blick zählt als fehlgeschlagen. Zeitplan 250/600/1000 ms, danach alle 300 ms bis
   2 s; 2 gleiche Ergebnisse entscheiden, nach 2 s entscheidet ein einzelnes, sonst „unbekannt“
   (wie dunkel). Unbekannte Fenster bekommen beim nächsten Fokus eine neue Runde (höchstens 3).
2. **Bereiche:** höchstens **32** Rechtecke (sichtbarer DWM-Rahmen, ohne unsichtbaren Rand) von vorn
   nach hinten in einem eigenen Constant Buffer (`b1`), dazu eine Palette aus 9 Matrizen (Monitor,
   Original, je Filter; Kontrast/Helligkeit eingerechnet). Der Shader nimmt das erste Rechteck, das
   den Quellpixel enthält – darüber liegende Fenster verdecken so die darunter. Fenster ohne eigene
   Wirkung bleiben nur als Abdeckung über gefilterten Fenstern erhalten; ganz verdeckte entfallen.
   Mehr als 32: die vorderen zählen, der Rest zeigt den Monitorfilter.
3. **Quelle:** WinEvents außerhalb des Prozesses (Vordergrund, Verschieben, Minimieren,
   Anzeigen/Verbergen/Z-Reihenfolge, Ort, Cloaking) auf einem eigenen Thread; jede Ereigniswelle
   endet in einer Aktualisierung (Anfrage wird hinter die Ereignisse gestellt). Der Render-Thread
   liest nur den unveränderlichen Snapshot; hochgeladen wird nur bei geändertem Snapshot.
4. **„Keiner“ schlägt auch den Monitorfilter** (wie ADR-0020 Nr. 4): Foto-/Video-Apps bleiben
   bei `Strg+Alt+C` original. Die **Windows-Oberfläche** dagegen folgt dem Monitorfilter; sie wird
   nur nie für sich gefiltert (auch keine Nutzerregel).
5. **Gemeinsam für alle Profile:** Automatik, Empfindlichkeit und Regeln liegen in `settings.json`
   (`WindowFilter`), nicht im Profil – Regeln beschreiben Apps, nicht eine Arbeitssituation.
6. **Sicherer Bildschirm:** Filter pro Fenster aus (`SecureSettingsSanitizer`), Kürzel unbelegt.
7. **Store-Apps:** Fensterart nach dem Prozess des `Windows.UI.Core.CoreWindow` im
   `ApplicationFrameWindow`, sonst hätten alle Store-Apps eine gemeinsame Regel.
8. **Kürzel:** sieht das aktive Fenster gefiltert aus (eigener Filter oder Monitorfilter), wird es
   „Keiner“, sonst der Automatik-Filter; Rückmeldung als kurze Tray-Meldung.

## Verworfene Alternativen
- **GPU-Messung (ADR-0020 Nr. 2):** weckt Render-Thread und Duplication, auch wenn nichts zu filtern ist.
- **`PrintWindow`:** misst auch verdeckte Fenster, ist aber langsam und lässt manche Apps neu zeichnen.
- **Bereiche als Textur/Maske:** beliebig viele Fenster, aber Maske pro Änderung neu zeichnen und eine
  Textur mehr pro Pixel; 32 Rechtecke reichen für reale Desktops (gemessen: 16 sichtbare Fenster).

## Konsequenzen
- ✅ Kein Fenster hell → Overlay schläft, keine Aufnahme; Prüfung neuer Fenster ohne GPU.
- ✅ Gemessen (Dev-PC): Aktualisieren 0,1 ms bei 16 Fenstern; ein Blick ≈ 16 ms auf dem
  Tracker-Thread (GDI wartet auf den DWM-Frame); `WindowRegionsCheck` 33 ns, 0 B.
- ⚠️ Beim Verschieben kann der Bereich einen Frame vor/hinter dem Bild liegen (Ereignis vs.
  Duplication-Frame) – Sichtprüfung durch einen Menschen nötig.
- ⚠️ Gesperrter Bildschirm/abgeschaltete Anzeige: Blicke sehen Schwarz → Fenster gilt als dunkel,
  bis es neu geöffnet wird.
