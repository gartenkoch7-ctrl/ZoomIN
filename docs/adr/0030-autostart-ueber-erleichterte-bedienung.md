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

## Nachtrag: Spike A1 (gescheitert, 2026-11-16)
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
- Ergebnis (2026-11-16, dev.280.m10, Abmelden/Anmelden): **gescheitert** – es gilt Q9.
  - Windows startet ZoomIN nach der Anmeldung aus der Liste (`Configuration = nvda_nvda_v1,ZoomIN`),
    ohne Win+U-Signal (Log 2328). Die Job-Instanz meldet `left_job = True` (Ablauf
    `zoomin.autostart.leave_job`), die neu gestartete Instanz aber 2327 „Still in a job“ mit
    Job-Limits `0x00000800` (nur `BREAKAWAY_OK`) – der Ausstieg bringt sie nicht aus dem Job.
  - Innerhalb von 5 s kamen **drei** Job-Instanzen (22:39:03, 22:39:08 zweimal): Windows startet
    das Hilfsmittel neu, sobald die Job-Instanz endet. Die erste neue Instanz fand weder Tray noch
    Grafik (Log 2503, 3003) und verschwand.
  - Beim ersten Bildschirmwechsel danach (UAC) verschwand die Tray-App ohne Logzeile (hartes Ende
    des Jobs), Sekunden später lief eine neue. Die Tray-App überlebt den Wechsel also nicht.
  - Nebenbefund: Beim Abmelden startet der Wächter ZoomIN neu (Exit-Code `0x40010004`, Log
    4002/4003) – er muss das Sitzungsende erkennen.

## Nachtrag: Ausweichweg Q9 umgesetzt (2026-11-16)
- Eine installierte ZoomIN startet über eine **geplante Aufgabe** `ZoomIN Autostart <SID>` im
  Stammordner der Aufgabenplanung: Trigger „bei Anmeldung“ nur dieses Benutzers,
  `InteractiveToken`, `LeastPrivilege`, Priorität 4 (normal), keine Laufzeitgrenze, auch im
  Akkubetrieb, `AllowHardTerminate=false`. Angelegt mit `schtasks /Create /XML` als der Benutzer
  selbst – ohne Adminrechte (Probe 2026-11-16).
- Die Aufgabenplanung kann die UIAccess-EXE nicht selbst starten (`0x800702E4`, Fehler 740). Die
  Aktion ist darum `rundll32.exe shell32.dll,ShellExec_RunDLL "<current>\ZoomIN.exe"`: ShellExecute
  lässt AppInfo UIAccess vergeben, rundll32 endet sofort, ZoomIN läuft weiter (Probe mit `osk.exe`
  und einem Pfad mit Leerzeichen).
- Die Liste der Erleichterten Bedienung wird für den Autostart nicht mehr genutzt. Die Migration
  zieht Run-Wert **und** Listeneintrag (Spike-Builds) in die Aufgabe um und löscht beide erst, wenn
  die Aufgabe existiert. Folge 2 („`EaseOfAccessNotifier` entfällt“) gilt nicht: Windows kennt die
  Aufgabe nicht als Hilfsmittel, der Win+U-Trick bleibt.
- „Im Job mit beendetem Elternprozess“ zählt nicht mehr als Start durch die Erleichterte Bedienung –
  so startet auch die Aufgabe (Job der Aufgabenplanung, rundll32 schon beendet).
- Wächter: Beim Abmelden (Exit-Code `0x40010004` oder `SM_SHUTTINGDOWN`) startet er nicht neu.
- Einmalig nach dem Update von einem Spike-Build: Startet die Liste ZoomIN noch über einen schon
  beendeten Broker, verlässt diese Instanz den Job nicht mehr und endet beim nächsten Win+L. Die
  Migration entfernt den Listeneintrag beim selben Start, ab der nächsten Anmeldung gilt die Aufgabe.
- Nachtest 2026-11-16 (dev.281.m10, Nutzer): Abmelden ohne Neustart durch den Wächter; nach der
  Anmeldung startet die Aufgabe genau eine Instanz (Ergebnis 0), der Umzug aus der Liste lief;
  Win+L vergrößert, die Tray-App überlebt. Das erste Tray-Symbol scheitert, solange die Taskleiste
  fehlt (Log 2503), und kommt mit `TaskbarCreated`.
- Befund danach (Nutzer): Nach dem Autostart blieben Sperre und Strg+Alt+Entf unvergrößert, nach
  einem Start aus dem Startmenü nicht. Ursache: Win+U ist ein Kürzel von Explorer; die Aufgabe
  startet ZoomIN vor der Taskleiste, das Start-Signal verpufft. Behoben: `EaseOfAccessNotifier`
  sendet es erst, wenn das Tray-Symbol in der Taskleiste ist (`ITrayIcon.Shown`), plus 2 s (Log 2315).
- Offen: Bei der Deinstallation bleibt die Aufgabe **anderer** Benutzerkonten stehen; deren
  Anmeldung meldet dann eine fehlende Datei. Aufräumen im erhöhten Schritt kommt später.
