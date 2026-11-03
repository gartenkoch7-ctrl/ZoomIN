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

## Offen (Spike mit ZoomIN selbst)
- Startet Windows ZoomIN bei Sperre/Strg+Alt+Entf/UAC, obwohl die Tray-App über unseren eigenen
  Autostart lief und nur HKLM-`Configuration` gesetzt ist? Falls nein: HKCU-`Configuration`
  prüfen (Nebenwirkung: Start nach der Anmeldung im Job).
- Gelten Tastenkürzel (`RegisterHotKey`) pro Desktop? Sonst würde die Tray-App auf `Default` die
  Kürzel der sicheren Instanz blockieren.
- Startet ATBroker den Wächter mit im Job, und wird er beim Wechsel mit beendet?
- Unter welchem Konto, in welcher Sitzung und auf welchem Desktop startet ATBroker das Programm?
- Startet es bei Sperre/UAC, wenn die Benutzerinstanz **nicht** über die Erleichterte Bedienung
  gestartet wurde (unser eigener Autostart)?
- Liegt ein Topmost-Fenster über LogonUI, Sperrbildschirm und UAC-Dialog?
- Wirkt `SetWindowDisplayAffinity` dort? Desktop Duplication (Runde 2, mit ZoomIN selbst).
- Wie lange dauert der Start?
