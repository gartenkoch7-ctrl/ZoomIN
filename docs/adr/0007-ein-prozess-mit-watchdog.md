# ADR-0007: Ein Prozess plus Watchdog

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Kontext
ZoomIN ist ein Hilfsmittel: Stürzt es ab, steht der Nutzer ohne Vergrößerung da – und ggf.
mit verstecktem Mauszeiger (M5) oder reserviertem Bildschirmplatz (M4).
Alternativen: nur ein Prozess; UI und Engine als getrennte Prozesse mit IPC.

## Entscheidung
App und Zoom-Engine laufen in **einem Prozess** (Render auf eigenem Thread). Ein kleiner
**Watchdog-Prozess** (`ZoomIN.Watchdog`, eigenes Projekt, referenziert nur Core) überwacht ihn.

## Aufgaben des Watchdogs
- Hauptprozess endet unerwartet → Systemzeiger wieder einblenden, AppBar-Reservierung
  freigeben, ZoomIN neu starten (max. 3 Neustarts in 5 Minuten, dann aufgeben + Hinweis).
- Reguläres Beenden meldet der Hauptprozess vorher ab (kein Neustart).
- Keine Logik außer Überwachen/Aufräumen/Neustarten; < 300 Zeilen gesamt anstreben.

## Konsequenzen
- Wird in **M3** gebaut, in M4 (AppBar) und M5 (Mauszeiger) um Aufräumschritte erweitert.
- Architekturtests um `ZoomIN.Watchdog` erweitern (nur Core erlaubt, plus Interop).
