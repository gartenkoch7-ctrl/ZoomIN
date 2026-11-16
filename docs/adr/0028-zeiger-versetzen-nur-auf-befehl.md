# ADR-0028: Zeiger versetzen nur auf ausdrücklichen Befehl

- **Status:** Angenommen (Umsetzung in [M19](../plan/m19-mehrere-ansichten.md))
- **Datum:** 2026-11-11

## Kontext
ZoomIN hat den Mauszeiger bisher nie versetzt (M6-Entscheidung, ADR-0003/0018). Der Nutzer
behält damit immer die Kontrolle, und Klicks landen genau dort, wo er hinzeigt. M19 bringt aber
Funktionen, die ohne Versetzen nicht gehen:
- Der Zeiger wechselt per Kürzel auf den nächsten Monitor (`Strg+Alt+N`).
- Beim Einfrieren eines Monitors (`Strg+Alt+H`) springt der Zeiger auf den nächstgelegenen freien
  Monitor.
- Ein eingefrorener Monitor ist für den Zeiger gesperrt. Dort passt das Bild nicht zum Klickpunkt,
  und so entstehen keine Fehlklicks.

## Entscheidung (Grilling 2026-11-11)
1. ZoomIN versetzt den Zeiger (`SetCursorPos`) **nur als direkte Folge eines Kürzels**, das der
   Nutzer drückt: Monitorwechsel und Einfrieren. Es gibt kein automatisches Versetzen, auch nicht
   bei Fokuswechsel.
2. ZoomIN darf den Zeiger aus Bereichen **heraushalten** („Zeigerzaun“: `ClipCursor` oder ein
   Maus-Hook, der Bewegungen verschluckt). Das gilt nur für Zustände, die der Nutzer selbst
   eingeschaltet hat: eingefrorener Monitor, Maus-Puffer, Achsensperre.
3. Der Zaun wird bei Beenden, Absturz (Watchdog), Desktopwechsel (Win+L, UAC, Strg+Alt+Entf) und
   Anzeigewechsel freigegeben bzw. neu berechnet. Er darf den Zeiger nie dauerhaft einsperren.
4. Jedes Versetzen und jede Zaunänderung wird protokolliert (Monitor-Index, Grund), ohne Inhalte.

## Begründung
- Versetzen nur auf Befehl bleibt vorhersehbar. Der Nutzer löst es selbst aus und erwartet den
  Sprung.
- Den Zeiger vom eingefrorenen Monitor fernzuhalten löst das Klickproblem einfacher und sicherer
  als jede Umrechnung.

## Folgen
- Die M6-Regel „Mauszeiger wird nie versetzt“ gilt weiter für alles Automatische.
- „Zeiger folgt Fokus“ (Idee A7) bleibt ausgeschlossen, solange kein neues ADR es erlaubt.
- Neue Plattform-Bausteine `IPointerFence` und ein schreibender Maus-Hook. Sie brauchen eine
  Ausnahmeliste, etwa für Spiele und Fernsteuerung, und Tests für die Freigabe in allen
  Fehlerfällen.
