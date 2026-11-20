# ADR-0033: Farbfilter-Kürzel schaltet bei „intelligent“ nur das aktive Fenster

- **Status:** Angenommen
- **Datum:** 2026-11-20
- **Ergänzt:** [ADR-0020](0020-filter-pro-fenster.md) (Regeln pro App + Fensterklasse) und
  [ADR-0024](0024-intelligent-invertieren.md)

## Kontext
Das Farbfilter-Kürzel (ab Werk Strg+Alt+C, beim Nutzer Strg+Leertaste) schaltet den Farbfilter des
Monitors unter der Maus an/aus. Bei „Invertiert
(intelligent)“ will der Nutzer meist nicht alles abschalten, sondern nur ein Fenster, das gerade
falsch aussieht, in Originalfarben sehen. Das vorhandene Kürzel „Filter für aktives Fenster an/aus“
(ADR-0020) ist ab Werk nicht belegt und setzt beim Zurückschalten einen festen Filter.

## Entscheidung (Nutzer, 2026-11-20)
1. **Wann:** Zeigt der Monitor des Fensters „intelligent“ und ist „Filter pro Fenster“ an, wirkt ein
   Druck auf das Farbfilter-Kürzel nur auf ein Fenster. Bei jedem anderen Filter (und ohne Filter pro
   Fenster) bleibt alles wie bisher: Monitor unter der Maus an/aus.
2. **Welches Fenster:** das Vordergrundfenster (Tastaturfokus), egal auf welchem Monitor. Ist es die
   Windows-Oberfläche (Desktop, Taskleiste) oder keins, das Fenster unter der Maus.
3. **Dauer:** gemerkt pro App + Fensterklasse wie beim Fensterfilter-Kürzel (Regel auf der Seite
   „Filter pro Fenster“ sichtbar, bleibt nach Neustart). Erster Druck: „Kein Filter“; nächster Druck:
   zurück auf „Automatisch“, damit das Fenster wieder dem intelligenten Monitorfilter folgt (kein
   fester Filter). Eine Titelleisten-Wahl der Seite bleibt.
4. **Ganz aus:** zweimal schnell (≤ 400 ms) drücken schaltet den Farbfilter des Monitors an/aus; die
   Regel des ersten Drucks wird dabei zurückgenommen. Tray und Einstellungen wie bisher.

## Konsequenzen
- ✅ Ein einzelnes Fenster lässt sich schnell aus dem intelligenten Filter nehmen.
- ⚠️ Ein einfacher Druck schaltet bei „intelligent“ den Monitor nicht mehr aus; der Doppeldruck muss
  im Handbuch stehen.
- ⚠️ Der Doppeldruck macht den ersten Druck rückgängig; dazwischen blitzt das Fenster kurz auf.
- Umsetzung: `FilterShortcutTarget` (Core, reine Logik), `FilterShortcut` (App, Kürzel-Thread),
  `WindowFilterCommands.ToggleOnSmartMonitor`, `IWindowFilterSource.TryGetAt`.
