# M7 – App, Einstellungen & Profile

**Ziel:** Alles, was bisher per JSON eingestellt wird, über eine schöne, selbst barrierefreie
Oberfläche bedienbar machen.
**Demo:** Tray-Icon → Einstellungen → Zoom, Modus, Filter live ändern; Profil per
`Strg+Alt+P` wechseln; Erststart-Assistent fragt nach Autostart.
**Voraussetzungen:** M4, M5, M6.

## Entscheidungen (Grilling 2026-10-30)
- Einstellungen wirken **sofort live** und werden entprellt gespeichert
- **X schließt in den Tray** (einmaliger Hinweis); Beenden über das Tray-Menü
- **Start still im Tray**; nur beim allerersten Start öffnet der **Erststart-Assistent**
- **Profile = Darstellung** (Modus, Stufen, Lupe, Andocken, Filter, Zeiger, Tracking);
  Tastenkürzel, Sprache, Autostart, Update-Prüfung gelten für alle Profile
- Profile **manuell** wechseln (`Strg+Alt+P`, Tray, Seite „Profile“)
- **Sprache wie Windows** (DE bei deutschem Windows, sonst EN), umstellbar
- Autostart im Assistenten (Vorauswahl ja); Update-Prüfung **opt-in** (Vorauswahl nein)

## Technische Entscheidungen bei der Umsetzung
- **Tray-Icon selbst gebaut** (`Shell_NotifyIcon`, eigener Thread, `TaskbarCreated`) statt
  Fremdbibliothek – keine Lizenzprüfung, keine Abhängigkeit.
- **Lokalisierung mit eigener Textklasse** (`App/Localization/Text*.cs`) statt `.resw`/MRT:
  Sprachwechsel zur Laufzeit ist bei unverpackten WinUI-Apps mit MRT unzuverlässig.
- **Einzelinstanz** per Mutex; zweiter Start öffnet die Einstellungen der laufenden Instanz.
- `DispatcherShutdownMode.OnExplicitShutdown`: die App lebt ohne Fenster weiter.
- **Update-Prüfung** gebaut, aber ohne Adresse inaktiv (GitHub-Repo-URL noch offen).
- App-Symbol `Assets/ZoomIN.ico` (Lupe, Bernstein), klassische DIB-Frames + 256 px PNG.

## Umsetzung
- Core: Setter mit `Changed` (live + speichern), `ZoomLevels.CopyFrom`, `ProfileFields`,
  `HotkeyRules` (Modifier, AltGr), `IProfileStore`, `IAutostart`, `IUpdateChecker`, `ITrayIcon`
- Platform: `Win32TrayIcon`, `RegistryAutostart` (HKCU Run), `JsonProfileStore`
  (`%AppData%\ZoomIN\profiles\*.json`), `GitHubUpdateChecker`, `UnregisterAll` für Hotkeys
- App: `SettingsService` (gemeinsame Einstellungen + Modelle → `settings.json` + aktives
  Profil), `ProfileService`, `AppShell` (Tray, Fenster, Beenden), `SettingsWindow` mit Seiten
  Allgemein · Zoom & Modi · Farben & Zeiger · Fokus & Textcursor · Tastenkürzel · Profile ·
  Info & Status, `WelcomePage` (5 Schritte), Hotkey-Editor (Aufnahme, AltGr-Sperre)

## Akzeptanzkriterien
- [x] Jede Einstellung aus M2–M6 in der UI änderbar und sofort wirksam (gebunden an Modelle)
- [x] Fenster schließen → läuft im Tray weiter; zweiter Start öffnet das Fenster (automatisch geprüft)
- [x] Alle Seiten per UI Automation erreichbar, **kein bedienbares Element ohne Namen**
- [x] Defekte `settings.json` → Standardwerte (seit M2); Profile mit ungültigem Namen abgelehnt
- [ ] Tray: Rechtsklick-Menü, Beenden (Exit-Code 0, kein Watchdog-Neustart) – Sichttest
- [ ] Narrator-Test (Sprachausgabe liest Seiten sinnvoll vor) – manuell
- [ ] Hotkey ändern, Profil wechseln/exportieren/importieren, Sprache umstellen – Sichttest
- [ ] Ohne Zustimmung keinerlei Netzwerkzugriff (ohne Update-Adresse ohnehin keiner)

## Offene Fragen / Risiken
- Schieberegler zeigen ihren Zahlenwert nur beim Ziehen (Tooltip) – ggf. Wert daneben anzeigen.
- Win-Kombinationen kann der Hotkey-Editor nicht immer aufnehmen (Shell fängt sie ab).
- Update-Adresse eintragen, sobald das GitHub-Repo feststeht.
