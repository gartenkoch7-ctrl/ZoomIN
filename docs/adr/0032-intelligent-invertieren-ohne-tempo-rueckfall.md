# ADR-0032: Intelligentes Invertieren ohne Rückfall wegen Tempo

- **Status:** Angenommen (ersetzt in ADR-0024 Entscheidung 6 den Rückfall `budget`; der Rückfall
  `gpu_feature` bleibt)
- **Datum:** 2026-11-20

## Kontext
ADR-0024 Nr. 6 schaltete „Invertiert (intelligent)“ auf „Invertiert, Farbton erhalten“ um, wenn die
gemessene GPU-Zeit das Budget riss (seit 2026-11-17: drei Mittel hintereinander über 20 % der
Bildzeit, `SmartBudget`). Der Rückfall galt bis zum Neuaufbau der Pipeline.

Befund im Nutzertest von 0.9.0 (Arbeitsrechner, Monitorfilter „intelligent“): Immer wieder war
plötzlich der **ganze Bildschirm invertiert**, auch dunkle Fenster, und blieb so, bis der Filter
ausgeschaltet wurde. Genau das zeigt der Rückfall: „Farbton erhalten“ invertiert alles. Nach jedem
Neuaufbau (Sperren, Monitorwechsel) lief „intelligent“ kurz, dann griff der Rückfall erneut.

Die Messung taugt nicht als Schalter: Zeitstempel zeigen bei heruntergetakteter oder geteilter GPU
(Leerlauf-Takt, Teams, Video) lange Zeiten, ohne dass ein Bild verloren geht (schon 2026-11-16 auf
einer RTX 3060 Ti: 1,68 ms live gegenüber 0,05–0,2 ms offline). Ein falscher Rückfall ist für
sehbehinderte Nutzer viel schlimmer als etwas weniger flüssiger Zoom: Dunkle Fenster werden grell.

## Entscheidung (Nutzer, 2026-11-20)
- „Intelligent“ bleibt **immer** intelligent, solange die GPU es einrichten kann.
- Die Messung bleibt: Metrik `zoomin.render.smart_invert_gpu_time`, und einmal je Pipeline die
  Warnung 3008 plus ein Hinweis auf der Diagnose-Seite („Grafik langsam … bleibt trotzdem
  intelligent“). Die erneute Messung des Magnify-Zusatzes vor dem Hinweis bleibt.
- Rückfall auf „Farbton erhalten“ nur noch, wenn Umgebungs-Durchgang oder Zeitmessung nicht
  angelegt werden können (`gpu_feature`, Log 3007).

## Konsequenzen
- ✅ Kein plötzlich voll invertierter Bildschirm mehr.
- ⚠️ Auf wirklich zu langsamer Grafik kann der Zoom ruckeln; der Hinweis auf der Diagnose-Seite und
  Log 3008 zeigen dann die Ursache. Der Nutzer kann selbst „Farbton erhalten“ wählen.
- `SmartInvertFallback` mit Grund `budget` heißt jetzt nur noch „langsam“ (`ShowsKeepHue` = false).
