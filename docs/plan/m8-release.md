# M8 – Release 1.0

**Ziel:** Eine stabile, gemessene, dokumentierte Version, die man herunterladen und im Alltag
nutzen kann.
**Demo:** GitHub-Release mit signiertem `ZoomIN-Setup.exe` und ZIP (seit M9, vorher ZIP + Inno); frische Windows-Installation → installieren →
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
- [ ] SignPath eingerichtet (aus M9): Antrag (OSS, [signpath-antrag.md](signpath-antrag.md)) gestellt
      und bewilligt; Secret `SIGNPATH_API_TOKEN`, Variablen `SIGNPATH_ORGANIZATION_ID`,
      `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`; erster signierter Lauf geprüft
- [ ] SignPath-Zertifikatsname in `TrustedPublishers`, Sperrlisten-Prüfung (`WTD_REVOKE_WHOLECHAIN`) an (aus M9)
- [ ] `ZoomIN-Setup.exe` aus CI auf frischem Windows 11 und Windows 10: Setup → UIAccess aktiv
      (Overlay über Ton-/Netzwerk-Menü), Deinstallation entfernt alles außer (auf Wunsch) Einstellungen (aus M9)
- [ ] Update Ende-zu-Ende mit signiertem Release: Klick in der App, Downgrade, Kanalwechsel,
      Einstellungen bleiben; Setup-Knopf der ZIP-Fassung startet das Setup (aus M9)
- [ ] Setup-Assistent per Tastatur und mit NVDA/Sprachausgabe, Kontrastdesign, 200 % Textgröße (aus M9)
- [ ] Windows-10-VM: Start, Zoom, Einstellungen
- [ ] Freigabetest Teams, Zoom/Webex, Discord, Browser (aus M1)
- [ ] 144 Hz, Win+L, Ruhezustand; Zweitmonitor (aus M1/M3)
- [ ] 8-h-Dauertest: kein Speicherwachstum, keine Ruckler
- [ ] Onboard-Grafik (aus M13): intelligentes Invertieren bei 1440p ≤ 1 ms extra GPU, kein Rückfall `budget` (Log 3007)
- [ ] Onboard-Grafik (aus M12): Filter pro Fenster bei 1440p – CPU ≤ 3 %, Bildrate = Monitor-Hz, ≤ 1 Frame Verzögerung, kein Versatz beim Ziehen
- [ ] Sichttests M4–M7, Narrator-Durchgang
- [ ] `v1.0.0-rc1` getaggt und veröffentlicht → nach den Tests `v1.0.0`

## So wird veröffentlicht (Stand 2026-11-17, Setup aus M9)
Voraussetzung: SignPath ist eingerichtet (Secret und Variablen im Repo, siehe oben). Ohne
`SIGNPATH_API_TOKEN` laufen Build und Tests, veröffentlicht wird nichts.
```powershell
git checkout main; git merge --no-ff develop                # nur mit Freigabe
git tag v1.0.0-rc1
git push origin main v1.0.0-rc1                             # startet release.yml
```
- `release.yml` → `build-release.yml`: Tests, UIAccess-Build, `ZoomIN-Setup.exe`
  (`publish-setup.ps1`), Signierung beider über SignPath (Freigabe von Hand),
  `package-release.ps1` (`ZoomIN-<v>-win-x64-uiaccess.zip`, `checksums.txt`), GitHub-Release
  (Tag mit „-“ = Vorabversion, Text aus `docs/release-notes/<tag>.md`).
- Die Namen der Assets müssen zu `ReleaseAssets` passen, sonst sieht der Release-Feed den Release
  nicht (KB [build-und-tooling.md](../kb/build-und-tooling.md#release-assets-und-versionen-adr-0014)).
- Danach `VERSION` hochsetzen, sonst sortieren neue develop-Builds vor dem Release.
- Für `v1.0.0`: `docs/release-notes/v1.0.0.md` anlegen (Tabelle aktualisieren), dann taggen.

## Offene Fragen / Risiken
- **2026-11-02:** Inno Setup wird durch das eigene Setup aus M9 ersetzt (ADR-0014); der erste
  Release erscheint erst danach. Die Abschnitte zu Inno Setup oben gelten nur bis dahin.
- Unsignierte EXE + Hooks + Overlay → mögliche Virenscanner-Fehlalarme; EXE bei Microsoft
  zur Analyse einreichen; Code-Signing (SignPath für OSS) nach dem MVP.
- Inno-Setup-Skript ist lokal nicht kompiliert worden (kein ISCC) – erster CI-Lauf prüft es.
