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

## Offen (Spike mit ZoomIN selbst)
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
