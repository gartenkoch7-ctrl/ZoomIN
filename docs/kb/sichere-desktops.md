# Sichere Desktops (Anmeldung, Sperre, Strg+Alt+Entf, UAC)

Gehört zu [ADR-0016](../adr/0016-zoom-auf-sicheren-bildschirmen.md) und
[M10](../plan/m10-sichere-bildschirme.md). Status: **Spike läuft** – Abschnitte mit „offen“ werden
mit Messwerten gefüllt.

## Grundlagen
- Anmeldung, Sperre, Strg+Alt+Entf/„Kennwort ändern“ und UAC-Abfragen laufen auf dem Desktop
  `winsta0\Winlogon`. Die Benutzerinstanz bleibt dabei auf `Default` stehen; Desktop Duplication
  liefert ihr `ACCESS_LOST`, bis der Benutzer zurück ist.
- Fremde Programme dürfen dort nur laufen, wenn Windows sie startet. Der offizielle Weg ist die
  Registrierung bei der Erleichterten Bedienung; gestartet wird über
  `C:\Windows\System32\ATBroker.exe` (Quelle: Microsoft Learn, „Ease of Access – Assistive
  Technology Registration“, https://learn.microsoft.com/en-us/windows/win32/winauto/ease-of-access---assistive-technology-registration).

## Registrierung (laut Microsoft-Doku)
Schlüssel `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Accessibility\ATs\<Name>`
(Namensvorschlag der Doku: `Firma_Produkt_v#`; Schreiben braucht Admin-Rechte).

| Wert | Typ | Bedeutung |
|---|---|---|
| `ApplicationName` | SZ | Anzeigename (auch `@dll,-id` für Übersetzungen) – Pflicht |
| `Description` | SZ | Beschreibung, < 512 Zeichen – Pflicht |
| `ATExe` | SZ | EXE-Name; daran erkennt Windows, ob das Hilfsmittel läuft – Pflicht |
| `StartExe` | SZ | Voller Pfad zum Start – Pflicht |
| `StartParams` | SZ | Argumente |
| `Profile` | SZ | XML `<HCIModel><Accommodation type="mild vision"/>…</HCIModel>` (Werte case-sensitiv) – Pflicht |
| `SimpleProfile` | SZ | z. B. `Magnifier` – Pflicht |
| `TerminateOnDesktopSwitch` | DWORD | fehlt/1: Start in einem Job, bei **jedem** Desktopwechsel beenden und neu starten. 0: kein Job, Instanz bleibt, auf dem neuen Desktop startet eine weitere, falls dort keine läuft |
| `CopySettingsToLockedDesktop` | DWORD | 1: Windows kopiert `HKCU\…\Accessibility\ATConfig\<Name>` beim Wechsel in den HKCU des sicheren Desktops |
| `PassiveAutoStartBehavior` | DWORD | 0 (Standard): „Nach der Anmeldung starten“ startet das Hilfsmittel auch nach UAC und Sperre. 1: nur einmal pro Sitzung bei der Anmeldung |
| `SecureDesktopAccommodation` | SZ | Ersatz auf dem sicheren Desktop (anderer AT-Name, `magnifierpane`, `none`) |

- **Wann startet Windows es auf dem sicheren Desktop?** Laut Doku, wenn es auf dem normalen
  Desktop bereits läuft (aus Sicht der Erleichterten Bedienung) oder für den Anmeldedesktop
  eingerichtet ist.
- **Autostart-Listen** (kommagetrennte AT-Namen im Wert `Configuration` unter
  `…\Windows NT\CurrentVersion\Accessibility`): HKCU = „nach der Anmeldung starten“,
  HKLM = „vor der Anmeldung starten“ (Anmeldebildschirm). Die HKLM-Variante ist nicht von
  Microsoft dokumentiert, nur durch Anleitungen zur Windows-Lupe belegt – Spike prüft.
- **Job-Erkennung:** `IsProcessInJob`. Ein im Job gestartetes Programm, das sich selbst beendet,
  soll die Erleichterte Bedienung informieren (`HKCU\…\AccessibilityTemp\<Name>` = 2 bzw. 3 beim
  Start, dann Win+U senden).
- **Warnung der Doku:** Alles aus diesen Schlüsseln ist vom Benutzer schreibbar – Werte prüfen,
  Längen begrenzen, keine DLL-Namen oder Befehle daraus ausführen. Gilt für uns auch für
  `settings.json` (ADR-0016).
- Dieselben Schlüssel sind ein bekannter Persistenz-/Angriffsweg (Sicherheitsforschung,
  MITRE T1546.008). Darum: `StartExe` zeigt nur in `Program Files`, nie in einen
  Benutzerordner.

## Umgesetzt (Stand 2026-11-03)
- `ZoomIN.exe --secure-desktop` startet nur die Engine (`SecureDesktopHost`), Einzelinstanz
  `Local\ZoomIN.SecureInstance`, Ende mit Strg+Alt+Umschalt+Z. Als normaler Benutzer gestartet
  läuft derselbe Modus auf dem normalen Desktop (Entwicklertest).
- Ablage als SYSTEM: `Environment.SpecialFolder.LocalApplicationData` = SYSTEMs Profil
  (`%SystemRoot%\System32\config\systemprofile\AppData\Local\ZoomIN`), darin `secure\settings.json`
  (Kopie) und `logs\`. Bewusst nicht `%ProgramData%` (Benutzer dürfen dort Ordner anlegen).
- Benutzerdateien liest `GuardedFileReader`: Datei öffnen, dann per `GetFinalPathNameByHandle`
  prüfen, dass Windows wirklich den erwarteten Pfad geöffnet hat; `nNumberOfLinks == 1`; ≤ 256 KB.
- Falle beim Testen: Eine Shell aus einer MSIX-Paket-App (z. B. ein Agent-Terminal) bekommt
  `AppData` umgeleitet (`…\Packages\<App>\LocalCache\Roaming`). Der Pfadvergleich lehnt das ab,
  der sichere Modus nimmt dann Standardwerte. Für den echten Start als SYSTEM nicht relevant.

- Wächter: `--watchdog <pid> --secure`. Er wartet nach dem Ende der Instanz 500 ms: Ein
  Desktopwechsel beendet den ganzen Job samt Wächter, ein Absturz nicht. Nach 3 Abstürzen in 5 min
  schreibt er `secure-gave-up.txt`; bis zum nächsten Windows-Start beendet sich der sichere
  Modus dann sofort wieder.
- Kein Schwarzbild: Bis die erste Desktopkopie vorliegt (`DesktopDuplicator.HasImage`), bleibt das
  Overlay unsichtbar.
- Rauchtest auf dem normalen Desktop: Kürzel, die die Tray-App schon belegt, meldet der sichere
  Modus als „already in use“ – dort erwartet, weil beide auf demselben Desktop laufen.

- Registrierung (`SecureDesktopRegistration`): `ATs\ZoomIN` mit `ApplicationName`,
  `Description`, `ATExe=ZoomIN.exe`, `StartExe=<Program Files>\ZoomIN\current\ZoomIN.exe`,
  `Profile` (mild/severe vision), `SimpleProfile=Magnifier`, `TerminateOnDesktopSwitch=1`, keine
  `StartParams`; dazu `ZoomIN` in HKLM `Configuration`. Merker der Nutzerwahl:
  `HKLM\SOFTWARE\ZoomIN\SecureDesktop` (DWORD). Geschrieben nur von erhöhten Schritten (Setup,
  `--apply-update`, `--secure-desktop-setting on|off`, Deinstallation).
- Erkennung ohne Argument: Läuft ZoomIN als SYSTEM, startet immer der sichere Modus.
- Falle beim Update (beobachtet 2026-11-03, dev.103 → dev.104): `--apply-update` führt die
  **bisher installierte** `ZoomIN.exe` aus. Kennt diese die Registrierung noch nicht, wird beim
  ersten Update auf eine M10-Version nichts registriert. Abhilfe von Hand:
  `current\ZoomIN.exe --secure-desktop-setting on` (erhöht) oder der Schalter in der App; ab dem
  nächsten Update registriert der erhöhte Schritt selbst.
- Falle (Sicherheitsprüfung): `SHGetKnownFolderPath` mit Benutzer-Token folgt der Ordnerumleitung
  aus HKCU `User Shell Folders` – die der Benutzer selbst setzen kann, auch auf `\\host\share`.
  Öffnet SYSTEM so einen Pfad, meldet sich Windows dort mit dem Computerkonto an (NTLM-Relay).
  Darum: `SessionUser` liest nur impersoniert, `LocalPathRule` + `DriveType.Fixed` vor dem ersten
  Zugriff, und `GuardedFileReader` lehnt `\\?\UNC\…` als Ergebnis ab.

## Messungen (2026-11-03, dev.104–dev.108, Windows 11 23H2)
- **Ohne Startmeldung startet Windows ZoomIN bei Sperre, Strg+Alt+Entf und UAC nie** (Win+L um
  12:33/12:36/12:42, mehrere UAC-Abfragen: kein Eintrag im SYSTEM-Log). Nur HKLM-`Configuration`
  reicht dafür nicht; sie wirkt nur auf dem Anmeldebildschirm (nach dem Abmelden: Start als
  SYSTEM, Engine nach 287 ms bereit).
- Abhilfe laut Doku, umgesetzt in `EaseOfAccessNotifier`: Die Tray-App setzt beim Start
  `HKCU\…\AccessibilityTemp\ZoomIN` = 3 und sendet Win+U, beim Beenden = 2. Nur wenn ZoomIN
  registriert ist (sonst öffnet Win+U nur die Einstellungen). Danach startet Windows ZoomIN bei
  jeder UAC-Abfrage als SYSTEM in der Benutzersitzung (Engine nach 170–290 ms bereit). Sichtbare
  Nebenwirkung laut Nutzer: keine; im Hintergrund starten Utilman und die Einstellungen-App kurz.
- **Konto:** SYSTEM, Sitzung des Benutzers – aber **ohne SeTcbPrivilege**: `WTSQueryUserToken`
  scheitert mit 1314 (`ERROR_PRIVILEGE_NOT_HELD`). Ersatz in `SessionUserToken`: Token des
  `explorer.exe` derselben Sitzung (`PROCESS_QUERY_LIMITED_INFORMATION`, `TOKEN_QUERY |
  TOKEN_IMPERSONATE | TOKEN_DUPLICATE`). Es ist das eigene Token des Benutzers, Impersonieren gibt
  also nicht mehr Rechte, als er ohnehin hat. Damit: „using the session user's settings“, und
  die Kopie in `…\systemprofile\…\ZoomIN\secure` entsteht.
- Falle: `SettingsService.Current.ZoomOn` meldet den **laufenden** Zoom (beim Start immer aus),
  nicht den gespeicherten Wert. Der sichere Modus liest darum die geladenen `AppSettings`
  (behoben in dev.109; vorher startete jede Sperre unvergrößert).
- Startzeit dev.109 (UAC, Zoom an): Engine nach 298 ms, Pipeline nach ~430 ms, **erstes
  vergrößertes Bild nach 1440 ms** – Ziel ≤ 1 s verfehlt; die Lücke von ~1 s liegt zwischen
  „Zoom pipeline ready“ und dem ersten Bild (Ursache offen: erstes Duplication-Bild oder Animation).
- Die Kopie für den Anmeldebildschirm entsteht erst beim ersten Start auf einem sicheren Desktop
  mit angemeldetem Benutzer. Vorher nimmt der Anmeldebildschirm Standardwerte (Zoom aus).
- Falle beim Messen: In einer Shell aus einer MSIX-App (Agent-Terminal) sind **Lesen und
  Schreiben** unter `%LocalAppData%`/`%AppData%` und HKCU umgeleitet. Das echte Benutzer-Log
  liest man über `\\localhost\C$\Users\<Name>\AppData\Local\ZoomIN\logs`. Ein Registry-Wert, der
  aus so einer Shell gesetzt wird, landet nicht im echten HKCU, ein Handtest der Startmeldung
  von dort beweist also nichts.
- Prüfen, ob wirklich gesperrt wurde: Ereignisanzeige `Microsoft-Windows-Winlogon/Operational`,
  Ereignis 811 mit Benachrichtigung (4) = Sperre, (5) = Entsperren.

## Startzeit, Prüf-Logs, Schnappschuss, SYSTEM-Logs (M10-Abschluss, Spur C, 2026-11-12)
- **Ursache der ~1 s Lücke (aus dem Code):** Log 1107 wartete auf `ZoomController.Status.IsVisible`.
  Der Render-Thread veröffentlicht seinen Status aber nur **einmal je Sekunde** (`FrameStatistics.SecondElapsed`,
  damit der Frame ohne Lock bleibt). Die Sekunde beginnt mit `RunSession`, also mit „Zoom pipeline ready“
  – genau die gemessenen ~430 ms + 1000 ms. Das Bild selbst kam früher; gemessen wurde zu spät.
- Dazu glitt die Stufe bisher von 1× hoch (Standard 150 ms bis 95 %, bis zum Einrasten ~2,4 × so lange).
  Jetzt setzt `SecureStartWatch.SkipZoomAnimation` die Animationszeit vor dem Einschalten auf 0 und
  stellt sie 1 s nach dem ersten Bild auf der Stufe (Zeit für die übrigen Monitore) bzw. nach 10 s
  zurück.
- Neue Messung: `RenderMilestones` (in `RenderModels`, ohne DI, Uhr `TimeProvider.System`) hält
  Pipeline bereit, erste Desktopkopie, erstes gezeigtes Bild und erreichte Stufe. Nur die sichere
  Instanz schaltet sie scharf (`Arm`); der Render-Thread prüft je Frame nur, bis alles gesetzt ist,
  in der Tray-App gar nicht (Benchmark `MilestoneBenchmarks`: 0 B, < 1 ns). Daraus Log 1107 und
  1150 und die Kinder `zoomin.secure.start.pipeline`/`.capture`/`.present`/`.zoom_in`. Die Kopie
  wird erst nach ihrem Frame gestempelt; ein langes `.capture` heißt: Desktop Duplication kam spät.
- **Messung (2026-11-12 18:33, Debug-Build, normaler Desktop, Bildschirm an, Zoom an, ein Monitor):**
  Prozessstart → Engine-Start ~360 ms (davon `zoomin.secure.settings` 169 ms) → Pipeline +160 ms →
  erste Desktopkopie +22 ms → erstes Bild und Zoomstufe im selben Frame (`.present`/`.zoom_in` 0 ms).
  **Erstes vergrößertes Bild ≈ 545 ms nach Prozessstart** (Ziel ≤ 1 s); eine frische Duplication
  liefert ihr erstes Bild bei eingeschaltetem Bildschirm sofort. Bestätigung als SYSTEM bei UAC/Win+L
  steht aus (Testbuild, Log 1107/1150 bei Stufe „Ausführlich“, sonst die Abläufe im `.trace.jsonl`).
- **Falle beim Rauchtest:** Ist der Bildschirm aus (Nutzer lange inaktiv, hier 17 min), komponiert DWM
  nichts; eine neu angelegte Desktop Duplication liefert dann **kein einziges** Bild (`AcquireNextFrame`
  nur `WAIT_TIMEOUT`, auch nicht mit `LastPresentTime == 0`). Der Rauchtest meldet dann 1106 nach 10 s.
  Startzeiten nur bei eingeschaltetem Bildschirm messen. Offen: Liefert eine frische Duplication bei
  **statischem**, aber eingeschaltetem Bild sofort ein erstes Bild? Die Zeit 1150 „first desktop copy“
  im Testbuild beantwortet das.
- Prüf-Logs 1151–1162 (`SecureDesktopChecks`): Desktop, Job, Wächter und Kürzel gleich beim Start (Windows beendet die Instanz beim Desktopwechsel ohne Vorwarnung, oft nach Sekunden); die Overlay-Prüfungen nach dem ersten Bild (+1 s), sonst nach 10 s oder beim Beenden-Kürzel.
  `WindowFromPoint` überspringt das Overlay (WS_EX_LAYERED + WS_EX_TRANSPARENT ist für die
  Trefferprüfung unsichtbar), darum läuft `Win32SecureDesktopProbe` die Z-Reihenfolge ab
  (`GetTopWindow`/`GW_HWNDNEXT`, sichtbar und nicht „cloaked“) und nimmt das erste Fenster, dessen
  Rechteck die Monitormitte enthält. Geprüft nur auf Monitoren, deren Mitte ein eigenes Overlay deckt.
- Die Prüfzeilen sind Information (wie 1105/1107): Bei Stufe „Normal“ (ab Warnung) erscheinen nur
  die Warnungen. Für die Checkliste vorher „Ausführlich“ wählen.
- Watchdog-PID: `IWatchdogLauncher.Start` gibt sie jetzt zurück (ShellExecute liefert das Handle).
- Strg+Alt+Umschalt+D (nur sicherer Modus, wie Strg+Alt+Umschalt+Z): Debug-Schnappschuss in
  `folders.Debug()\secure-snapshot-<Zeit>` – als SYSTEM in dessen Profil, nur für Admins lesbar.
  AltGr+D erzeugt auf deutscher Tastatur kein Zeichen. Jeder am Anmeldebildschirm kann das Kürzel
  drücken: Es bleiben höchstens 3 Schnappschüsse, ältere löscht der nächste. Offen: Belegt der
  Benutzer Strg+Alt+Umschalt+D (oder +Z) mit einem eigenen Kürzel, fehlt es auf der Sperre (Log 1166/1104);
  der Kürzel-Editor kennt die festen Kürzel des sicheren Modus nicht. Ebenfalls offen:
  `HotkeyRules` kennt nur deutsche AltGr-Zeichen; auf US-International tippt AltGr+Umschalt+D „Ð“
  (und +Z „Æ“) – dort blockieren beide Kürzel ein Zeichen im Kennwortfeld.
- Rauchtest als normaler Benutzer (dev-Build, Terminal-Panel, nicht die Agent-Shell): Desktop
  `Default`, nicht im Job, Wächter läuft (nicht im Job), 16 Kürzel „unavailable“ (die Tray-App hält
  sie – erwartet), Beenden- und Schnappschuss-Kürzel registriert.
- **SYSTEM-Logs ins Diagnosepaket:** Kontrollkästchen auf der Diagnoseseite → `ElevatedSecureLogsRequest`
  startet `ZoomIN.exe --copy-secure-logs <Diagnose>\secure-logs-<GUID>` mit „runas“. Der erhöhte
  Schritt (`SecureLogCopy`) schreibt als Admin in einen Ordner des Benutzers, darum:
  - Ziel nur genau `<eigener Diagnoseordner>\secure-logs-<32 hex>` (`SecureLogCopyRule`), Laufwerk fest.
  - Diagnoseordner wird ohne Folgen von Reparse-Points geöffnet; `GetFinalPathNameByHandle` muss dem
    erwarteten Pfad entsprechen (kein Junction irgendwo). Danach **nur noch über das Handle**:
    Zielordner und jede Datei entstehen mit `NtCreateFile` relativ zum Handle (`RootDirectory`,
    `FILE_CREATE`, `FILE_OPEN_REPARSE_POINT`, `OBJ_DONT_REPARSE`; `Interop/NtFiles`). Falle
    (Sicherheitsprüfung): Teilen nur Lesen verhindert zwar Umbenennen und Löschen, aber nicht, dass
    der Benutzer den (leeren) Ordner zum Mount Point macht – `FSCTL_SET_REPARSE_POINT` braucht nur
    `FILE_WRITE_ATTRIBUTES`, und das regeln Freigabemodi nicht. Pfadbasiertes `CreateDirectory` danach
    wäre umleitbar.
  - Zielordner mit eigener Zugriffsliste (Besitzer Administratoren, geschützt; Benutzer nur Lesen und
    Löschen, also kein Eintrag und kein Link darin); existiert der Name in irgendeiner Form, Abbruch
    (Exit-Code 2). Nur `*.log`/`*.jsonl` aus SYSTEMs `logs` und `debug`, neueste zuerst, zusammen
    höchstens 20 MB.
  - Der Schritt startet **keine Telemetrie**: Ihr Log läge als Administrator im `%LocalAppData%\ZoomIN\logs`
    des Benutzers, das er per Junction/Objekt-Link umleiten kann (Anlegen/Anhängen und Löschen
    beliebiger Dateien als Admin). Das Ergebnis meldet der Exit-Code, die Tray-App loggt 2340.
    **Offen (vorbestehend, nicht Spur C):** Die übrigen erhöhten Rollen (`--secure-desktop-setting`,
    `--apply-update`, Deinstallation) loggen weiter dorthin – eigene Aufgabe vor `v1.0.0`.
  - Das Paket legt sie unter `secure/` ab und löscht die Kopie danach. Abgelehnte UAC-Abfrage →
    Vermerk in `ZoomIN-Info.txt`.
  - Grenze: Bei Erhöhung mit einem **anderen** Admin-Konto (Over-the-shoulder) ist der Diagnoseordner
    des erhöhten Kontos ein anderer → Ziel abgelehnt, Vermerk „copying them failed“.

## Offen (Spike mit ZoomIN selbst)
Plan zum Abschluss (Autostart, Kern-Modus, Prüf-Logs, Startzeit): [M10 „Abschluss“](../plan/m10-sichere-bildschirme.md#abschluss-grilling-2026-11-12), [ADR-0030](../adr/0030-autostart-ueber-erleichterte-bedienung.md).

- Start belegt (dev.108, 12:53): Strg+Alt+Entf und Win+L starten ZoomIN als SYSTEM mit den
  Benutzereinstellungen. Sichtprüfung dev.109 (12:57, Nutzer): Strg+Alt+Entf und Sperre
  vergrößert, erstes Bild nach 1402–1423 ms.
- ~1 s zwischen Pipeline bereit und erstem vergrößerten Bild.
- Gelten Tastenkürzel (`RegisterHotKey`) pro Desktop? Sonst würde die Tray-App auf `Default` die
  Kürzel der sicheren Instanz blockieren.
- Startet ATBroker den Wächter mit im Job, und wird er beim Wechsel mit beendet?
- Liegt ein Topmost-Fenster über LogonUI, Sperrbildschirm und UAC-Dialog?
- Wirkt `SetWindowDisplayAffinity` dort? Desktop Duplication (Runde 2, mit ZoomIN selbst).
- Wie lange dauert der Start?
