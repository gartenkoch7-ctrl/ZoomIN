# ADR-0025: Statische Klassen nur ohne Nebenwirkungen – Dienste über Interfaces und DI

- **Status:** Angenommen (Umsetzung in [M14](../plan/m14-interfaces-und-performance.md))
- **Datum:** 2026-11-07

## Kontext
ZoomIN hat rund 110 statische Klassen. Die meisten sind unproblematisch: reine Rechenlogik in
Core (`ViewportCalculator`, `ModeGeometry`, `WindowLayout`), `[LoggerMessage]`-Klassen und
Messinstrumente nach ADR-0019, Interop, Einstiegspunkte und Erweiterungsmethoden der
Composition Root.

Etwa 20 statische Klassen haben aber **Nebenwirkungen**. Sie lesen oder schreiben Dateien und die
Registry, starten Prozesse (teils mit Adminrechten), zeigen Dialoge oder lesen Pfade und die Uhr.
Beispiele: `RecoveryFiles` (Core liest `Environment.GetFolderPath`), `InstallRegistration`,
`ShellShortcut`, `ElevatedStep`, `WatchdogStarter`, `MessageDialog`, `UninstallFlow`,
`SecureDesktopGiveUp`. Dazu kommt **globaler Zustand**: die Sprache in `Text`/`SetupText` als
statisches Feld mit statischem `event`, zwei statische `HttpClient` und `Stopwatch.GetTimestamp()`
in `EffectsModel`.

Daraus folgt:
- Diese Abläufe sind nicht testbar. Deinstallation, Update und Watchdog haben keinen einzigen
  Test, und App, Setup und Watchdog haben kein Testprojekt.
- Die Abhängigkeiten sind versteckt: Die Pfade `%LocalAppData%\ZoomIN` und
  `%AppData%\ZoomIN` werden an etwa 16 Stellen zusammengebaut.
- Tests können sich über globalen Zustand gegenseitig beeinflussen.

## Entscheidung (Grilling 2026-11-07)
1. **Static bleibt erlaubt** für:
   - reine Funktionen ohne Zustand und ohne IO (Core-Logik, Konstanten-Structs mit `Fill`,
     Mathe im Render-Loop)
   - `[LoggerMessage]`-Klassen, `Telemetry` sowie Messinstrumente und Helfer für Abläufe (ADR-0019)
   - P/Invoke in `Interop/` und CsWin32
   - Einstiegspunkte (`Program`) und DI-Erweiterungsmethoden in `Composition/`
   - reine Datentabellen (Texte)
   - Win32-Rückrufe, die technisch einen statischen Einsprung brauchen, z. B. Low-Level-Hooks
     ohne Kontextzeiger. Der statische Teil leitet dabei nur an eine Instanz weiter.
2. **Alles mit Nebenwirkungen ist ein Dienst.** Dazu zählen Dateien, Registry, Prozesse,
   Dialoge, Netzwerk, Umgebungsvariablen, Pfade, Uhr und Sprache. Ein Dienst ist ein Interface
   in Core (oder im nutzenden Projekt, wenn nur dieses es braucht) und eine Implementierung in
   Platform/App. Beide werden in der Composition Root registriert.
   - Zeit kommt aus `TimeProvider`.
   - Pfade kommen aus einem `IAppFolders`.
   - Den `HttpClient` gibt es einmal, aus DI.
3. **Sprache:** `ILocalizer` ist ein Dienst, gemeinsam für App und Setup. Die statische Klasse
   `Text` bleibt nur als dünne Fassade für `x:Bind` in XAML und liest aus dem Dienst. Code
   außerhalb von XAML nutzt den Dienst.
4. **Render-Loop:** Dienste werden einmal in den Konstruktor gegeben und als `readonly`-Feld
   gehalten, nie pro Frame aus dem Container geholt. Pro Frame sind nur wenige virtuelle Aufrufe
   erlaubt (Uhr, Zähler für Anzeigeänderungen) und keine Allokation. Reine Mathe bleibt static.
5. **Settings ↔ Modell-Mapper** (`ZoomModelFactory`, `EffectsFactory`, `TrackingFactory`) sind
   Logik: Sie wandern nach Core und werden dort getestet.
6. **Durchsetzung:** Ein Architekturtest verbietet statische Klassen, die `System.IO.File`,
   `Directory`, `Registry`, `Process`, `Environment.GetFolderPath`/`GetEnvironmentVariable` oder
   `HttpClient` nutzen. Ausgenommen sind `Interop/`, `Program`, `Composition/` und eine kleine,
   im Test begründete Ausnahmeliste. Dialoge, Uhr und Sprache prüft das Code-Review; das
   lässt sich ohne viele Fehlalarme nicht automatisch erkennen. AGENTS.md Abschnitt 2.6
   bekommt die Regel.

## Folgen
- **Positiv:**
  - Abläufe wie Update, Deinstallation, Watchdog und Diagnosepaket lassen sich mit
    gefälschten Diensten testen.
  - Es gibt neue Testprojekte für App, Watchdog und Setup.
  - Die Abhängigkeiten stehen im Konstruktor.
  - Core bleibt frei von Umgebung und Uhr.
- **Negativ:**
  - Mehr Typen (Interface und Implementierung).
  - Konstruktoren werden länger. Das wird bewusst durch Aufteilen von `ZoomController`,
    `AppShell` und `PageContext` aufgefangen.
  - Wenige zusätzliche virtuelle Aufrufe im Render-Loop (geschätzt unter 50 ns pro Frame);
    ein Benchmark belegt `0 B`.
- **Nicht betroffen:** ADR-0019 bleibt gültig. `Telemetry`, `*Log.cs` und die Messinstrumente
  bleiben static.
