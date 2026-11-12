# ADR-0030: Autostart über die Erleichterte Bedienung

- **Status:** Angenommen (Umsetzung im [M10-Abschluss](../plan/m10-sichere-bildschirme.md#abschluss-grilling-2026-11-12))
- **Datum:** 2026-11-12
- **Ergänzt:** [ADR-0016](0016-zoom-auf-sicheren-bildschirmen.md)

## Kontext
Bisher startet ZoomIN nach der Anmeldung über den Run-Eintrag in HKCU (`RegistryAutostart`).
Das hat zwei Nachteile:
- **Später Start:** Windows 11 startet Run-Einträge verzögert, erst einige Sekunden nach der
  Anmeldung. Für eine Lupe ist das zu spät.
- **Windows kennt ZoomIN nicht:** Windows startet ein Hilfsmittel bei Sperre, Strg+Alt+Entf und UAC
  nur, wenn es glaubt, dass es schon läuft. Ein per Run-Eintrag gestartetes ZoomIN kennt es nicht.
  Darum setzt die Tray-App heute `AccessibilityTemp\ZoomIN` und sendet einen künstlichen Win+U
  (`EaseOfAccessNotifier`). Dieser Trick ist anfällig und startet nebenbei kurz Utilman und die
  Einstellungen-App.

NVDA nutzt den offiziellen Weg und steht in der Liste „nach der Anmeldung starten“ der Erleichterten
Bedienung (HKCU `…\Accessibility\Configuration`). Das ist auf dem Testrechner beobachtet, 2026-11-12.

## Entscheidung (Grilling 2026-11-12)
1. **Installierte ZoomIN startet über die Erleichterte Bedienung.** „Mit Windows starten“ trägt
   `ZoomIN` in die HKCU-Liste `Configuration` ein. Andere Einträge dort (z. B. NVDA) bleiben
   unverändert. Dafür sind keine Adminrechte nötig. Damit startet Windows ZoomIN sofort nach der
   Anmeldung und kennt es als laufendes Hilfsmittel, und der Win+U-Trick entfällt.
2. **Tray-App außerhalb des Jobs.** Windows startet Hilfsmittel aus dieser Liste in einem Job.
   Mit `TerminateOnDesktopSwitch=1` würde jede Sperre die Tray-App beenden. Ein Spike klärt den
   Weg. Kandidaten sind:
   - Die Instanz im Job startet die Tray-App mit `CREATE_BREAKAWAY_FROM_JOB` neu und beendet sich.
   - `TerminateOnDesktopSwitch=0` (kein Job; die sichere Instanz bliebe dann auf dem
     Winlogon-Desktop stehen, was auch die Startzeit verkürzen könnte).
3. **Ausweichweg ohne neue Rückfrage:** Gelingt das nicht sauber, startet eine geplante Aufgabe
   ZoomIN bei der Anmeldung ohne Verzögerung, und der Win+U-Trick bleibt. Das Ergebnis des Spikes
   kommt als Nachtrag in dieses ADR.
4. **Zwei Schalter**, beide im Setup vorausgewählt:
   - „Mit Windows starten“ (HKCU, ohne Adminrechte)
   - „Auch auf Anmelde- und Sperrbildschirm“ (HKLM, mit Admin-Abfrage wie bisher). Aus bedeutet:
     Die HKLM-Liste enthält ZoomIN nicht, und `SecureDesktopAccommodation=none` verhindert den Start
     bei Sperre und UAC, obwohl ZoomIN als Hilfsmittel läuft.
5. **Nicht installiert** (portables ZIP, Entwickler-Build): Hier gibt es keine Registrierung bei der
   Erleichterten Bedienung, weil sie Adminrechte und `Program Files` braucht. „Mit Windows starten“
   nutzt dort weiter den Run-Eintrag.
6. **Bestehende Installationen** stellen einmal um: Beim ersten Start der neuen Version wird ein
   vorhandener Run-Eintrag entfernt und durch den Eintrag in der Liste ersetzt. Ohne Run-Eintrag
   bleibt der Autostart aus. Das wird geloggt, einen Hinweis gibt es nicht.

## Begründung
- Der offizielle Weg startet am frühesten und ohne simulierte Tasten. So machen es auch andere
  Hilfsmittel, und Windows zeigt den Schalter zusätzlich in seinen Einstellungen an.
- Zwei Schalter trennen „bequem starten“ (ohne Adminrechte) von „auf sicheren Bildschirmen laufen“
  (sicherheitsrelevant, mit Adminrechten).

## Folgen
- `IAutostart` bekommt eine zweite Implementierung. Welche gilt, entscheidet `InstallLayout`.
- `EaseOfAccessNotifier` entfällt, wenn der Spike gelingt.
- Die Deinstallation entfernt den Eintrag aus der HKCU-Liste des Benutzers, der deinstalliert.
  Andere Benutzerkonten behalten einen toten Eintrag; Windows ignoriert Namen ohne Registrierung
  unter `ATs`.
- Windows zeigt ZoomIN in „Einstellungen › Barrierefreiheit“ bei den Hilfsmitteln, die nach der
  Anmeldung starten. Schaltet der Nutzer es dort aus, zeigt die App das beim nächsten Öffnen der
  Seite „Allgemein“ richtig an (der Schalter liest die Liste, keinen eigenen Merker).

## Nachtrag: Spike A1 (offen)
Stand 2026-11-12: Kandidat 1 ist gebaut und meldet sein Ergebnis selbst ins Log, die Messung mit dem
Nutzer steht aus. Ablauf und gesuchte Logzeilen:
[sichere-desktops.md › Spike A1 durchführen](../kb/sichere-desktops.md#spike-a1-durchführen-nutzer-offen).
- Gebaut: Prüfung auf Job und Elternprozess vor dem Einzelinstanz-Mutex; bei erlaubtem Ausstieg
  Neustart mit `CREATE_BREAKAWAY_FROM_JOB` und Marker `--left-job`; der Win+U-Trick nur, wenn die
  Erleichterte Bedienung nicht selbst gestartet hat.
- Nicht gebaut: der Ausweichweg aus Q9 (geplante Aufgabe). Er kommt nur, wenn der Spike scheitert.
- Ergänzung zu Folge 1 (Umsetzung): Die Liste wird nur genutzt, wenn HKLM `ATs\ZoomIN` existiert;
  sonst bleibt der Run-Eintrag, bis ein erhöhter Schritt der neuen Version ihn geschrieben hat
  (`--apply-update` läuft mit der alten Version).
- Ergebnis: _offen_ (ja/nein, Job-Limits, Elternprozess, Verhalten bei Win+L, UAC, nach „Beenden“).
