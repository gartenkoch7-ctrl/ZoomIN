# M8 – Release 1.0

**Ziel:** Eine stabile, gemessene, dokumentierte Version, die man herunterladen und im Alltag
nutzen kann.
**Demo:** GitHub-Release mit ZIP und Installer; frische Windows-Installation → installieren →
funktioniert.
**Voraussetzungen:** M7.

## Entscheidungen (Grilling 2026-10-30)
- **ZIP + Inno-Setup-Installer**, **nur x64**, **pro Benutzer** (`{autopf}` mit
  `PrivilegesRequired=lowest` → `%LocalAppData%\Programs\ZoomIN`, keine Admin-Rechte)
  → [ADR-0009](../adr/0009-verteilung-zip-und-inno-setup.md)
- Installer wird **in CI gebaut** (Inno Setup per Chocolatey); lokal entsteht die ZIP
- Version **1.0.0-rc1**, danach unverändert **1.0.0**, sobald die offenen Nutzertests durch sind
- **Keine Beta**; Release-Notes mit ehrlicher Tabelle getestet/ungetestet; Issue-Vorlage
- **GitHub-Repo-URL später**: Platzhalter `OWNER/ZoomIN` in `README.md` ersetzen; Update-Adresse
  in den Einstellungen eintragen

## Umsetzung
- **Stabilität:** Render-Watchdog in `ZoomEngine` (Herzschlag je `MonitorRenderer`; > 2 s ohne
  Fortschritt → Neuaufbau; Thread hängt weiter → `Environment.FailFast`, Prozess-Watchdog
  räumt auf und startet neu). Unbehandelte Fehler aller Threads werden geloggt.
- **Größe:** statt des Sammelpakets nur `Microsoft.WindowsAppSDK.WinUI` + `.Foundation` –
  ZIP 91 → **66 MB**, Ordner 235 → 173 MB (KI/ML/Suche entfallen).
- **Verteilung:** `scripts/publish.ps1` (self-contained win-x64, ZIP, Installer falls
  Inno Setup vorhanden), `installer/ZoomIN.iss` (de/en, optionaler Autostart, Deinstallation
  beendet ZoomIN + Wächter und entfernt den Autostart-Eintrag).
- **CI/Release:** `.github/workflows/release.yml` – Tag `v*` → Zeilenprüfung, Tests, Publish,
  Installer, GitHub-Release (Tag mit „-“ = Vorabversion, Text aus `docs/release-notes/<tag>.md`).
- **Doku:** `README.md`, `CONTRIBUTING.md`, `docs/benutzer/handbuch.md` + `manual.md`,
  `docs/release-notes/v1.0.0-rc1.md`, `.github/ISSUE_TEMPLATE/hardware-bericht.yml`.

## Akzeptanzkriterien
- [x] Release-Build (`artifacts/publish`) läuft eigenständig: Zoom an < 25 ms, 0 % CPU im
      Leerlauf, Strg+Mausrad, Watchdog (automatisch geprüft)
- [x] 138 Tests grün, Zeilenregel eingehalten
- [ ] Installer in CI gebaut und auf frischem Windows 11 installiert/deinstalliert
- [ ] Windows-10-VM: Start, Zoom, Einstellungen
- [ ] Freigabetest Teams, Zoom/Webex, Discord, Browser (aus M1)
- [ ] 144 Hz, Win+L, Ruhezustand; Zweitmonitor (aus M1/M3)
- [ ] 8-h-Dauertest: kein Speicherwachstum, keine Ruckler
- [ ] Sichttests M4–M7, Narrator-Durchgang
- [ ] `v1.0.0-rc1` getaggt und veröffentlicht → nach den Tests `v1.0.0`

## So wird veröffentlicht
```powershell
git remote add origin https://github.com/OWNER/ZoomIN.git   # einmalig
git push -u origin main
git tag v1.0.0-rc1
git push origin v1.0.0-rc1                                  # startet release.yml
```
Für `v1.0.0`: `docs/release-notes/v1.0.0.md` anlegen (Tabelle aktualisieren), dann taggen.

## Offene Fragen / Risiken
- **2026-11-02:** Inno Setup wird durch das eigene Setup aus M9 ersetzt (ADR-0014); der erste
  Release erscheint erst danach. Die Abschnitte zu Inno Setup oben gelten nur bis dahin.
- Unsignierte EXE + Hooks + Overlay → mögliche Virenscanner-Fehlalarme; EXE bei Microsoft
  zur Analyse einreichen; Code-Signing (SignPath für OSS) nach dem MVP.
- Inno-Setup-Skript ist lokal nicht kompiliert worden (kein ISCC) – erster CI-Lauf prüft es.
