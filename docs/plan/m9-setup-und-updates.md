# M9 – Setup & Updates

**Ziel:** ZoomIN lässt sich auf jedem Windows-PC barrierefrei installieren, mit UIAccess
(Zoom auch über Ton-/Netzwerk-Menü, Startmenü, Info-Center) und aus der App heraus aktualisieren.
**Demo:** Frisches Windows → `ZoomIN-Setup.exe` → Kanal und Version wählen → installieren →
Zoom liegt über den Schnelleinstellungen. Danach Update per Klick und Downgrade auf eine
ältere Version.
**Voraussetzungen:** M8 (Release-Workflow), UI-Neugestaltung. Kommt **vor** dem ersten Release.
→ [ADR-0014](../adr/0014-eigenes-setup-mit-kanaelen-und-updates.md)

## Entscheidungen (Grilling 2026-11-02)
| Thema | Entscheidung |
|---|---|
| Technik | Eigenes Projekt `ZoomIN.Setup` (C#/WinUI 3), kleines Online-Setup |
| Barrierefreiheit | Tastatur + Screenreader (NVDA, JAWS, Sprachausgabe), Kontrast, Textgröße, eigene Vergrößerung |
| Ort/Rechte | Immer `Program Files` + UIAccess; UI unerhöht, Kopieren in erhöhtem Hilfsprozess |
| Kanäle | Stabil (Releases) · Experimentell (RCs + `x.y.z-dev.N` je develop-Push) |
| Auswahl | Im Setup und in den Einstellungen (Seite „Updates“) |
| Downgrade | Erlaubt, Einstellungen und Profile werden vorher gesichert |
| Updates | Hinweis + Ein-Klick-Update, eine Admin-Abfrage pro Update |
| Update-Prüfung | Beim Start + täglich, im Setup vorausgewählt, abwählbar |
| Wizard-Schritte | Sprache, Kanal, Version, Autostart, Desktop-Verknüpfung, Lupen-Grundeinstellungen, Update-Prüfung |
| Erststart-Assistent | Bleibt als Fallback (übersprungen, wenn das Setup schon eingestellt hat) |
| Deinstallation | Über „Apps“ oder Setup; fragt, ob Einstellungen bleiben (Standard: behalten) |
| Sprachen | Deutsch + Englisch, folgt Windows, im ersten Schritt umstellbar |
| Altlasten | Portables ZIP bleibt (ohne UIAccess); Inno Setup entfällt, alte Installationen werden entfernt |
| Signatur | SignPath für OSS, auch develop-Builds; bis zur Genehmigung lokal testen |

## Aufgaben
### Fundament
- [ ] SignPath-Antrag stellen (OSS) – vorbereitet in [signpath-antrag.md](signpath-antrag.md). Geklärt: jede
      Signierung braucht eine Freigabe von Hand, auch develop-Builds. Blocker: es gibt noch keinen Release.
- [x] Core (`ZoomIN.Core.Updates`): `AppVersion` (SemVer-Reihenfolge inkl. `-rc`/`-dev.N`),
      `ReleaseChannel`, `ReleaseInfo`, `ReleaseCatalog` (Kanalfilter, Update, Downgrade-Erkennung),
      `ReleaseAssets`, `ReleaseFeeds` – mit Unit-Tests
- [x] Platform: `GitHubReleaseFeed` + testbarer `GitHubReleaseParser` (Liste statt nur `latest`)
- [x] Platform: `HttpPackageDownloader` (Fortschritt, SHA-256 beim Streamen, nur passende Datei
      bleibt liegen) und `AuthenticodeVerifier` (WinVerifyTrust + festgenagelter Herausgeber aus
      `TrustedPublishers`); Core: `ChecksumList`, `PackageCheck`
- [ ] SignPath-Zertifikatsname in `TrustedPublishers` eintragen, Sperrlisten-Prüfung
      (`WTD_REVOKE_WHOLECHAIN`) dann einschalten – das lokale Zertifikat hat keine Sperrliste
- [x] App: alten `IUpdateChecker`/`GitHubUpdateChecker` durch `IReleaseFeed` ersetzt
- [x] Ordnerstruktur `app-<version>` + Link `current` ([ADR-0015](../adr/0015-versionsordner-und-current-link.md)),
      Admin-Schritt `--apply-update` (`UpdateApplier`), Neustart mit `--wait-for`

**Pflicht-Assets je Release** (sonst ignoriert der Feed den Release):
`ZoomIN-<version>-win-x64-uiaccess.zip` (signierter UIAccess-Build) und `checksums.txt`
(SHA-256 aller Assets). Kanal folgt der Version: mit Vorabkennung → Experimentell.

### CI
- [x] `build-release.yml` (wiederverwendbar): Tests, portables ZIP, UIAccess-Build unsigniert →
      SignPath → `package-release.ps1` (Paket + `checksums.txt`). Ohne Secret
      `SIGNPATH_API_TOKEN` wird gebaut, aber **nichts veröffentlicht**.
- [x] `release.yml` (Tag `v*`) und `develop.yml` (jeder Push: `<VERSION>-dev.<Commit-Anzahl>`)
      nutzen ihn; Basisversion steht in der Datei `VERSION`
- [ ] SignPath einrichten: Secret `SIGNPATH_API_TOKEN`, Variablen `SIGNPATH_ORGANIZATION_ID`,
      `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`; ersten Lauf prüfen (Schritt ungetestet)
- [x] Setup als Release-Asset: `publish-setup.ps1` → `ZoomIN-Setup.exe` (Single-File, 67 MB)
- [x] SignPath signiert auch `ZoomIN-Setup.exe`: eine Anfrage mit `uiaccess/` + `setup/`, Action `@v3`;
      `package-release.ps1` lehnt ein unsigniertes Setup ab (Schritt ungetestet bis SignPath läuft)
- [x] Inno-Setup-Schritt und `installer/ZoomIN.iss` entfernt

### Setup-Programm (`ZoomIN.Setup`)
- [x] Projekt `ZoomIN.Setup` angelegt (referenziert nur Core und Platform; Logik liegt dort und ist getestet)
- [x] Wizard: Willkommen/Sprache, Kanal/Version, Einstellungen, Installation, Fertig; Sprache umschaltbar;
      eigene Vergrößerung (Größer/Kleiner, Strg+Plus/Minus/0, Strg+Mausrad); Überschrift und Status als
      Live-Region (in WinUI muss `LiveRegionChanged` selbst ausgelöst werden – auch auf der Updates-Seite nachgezogen)
- [x] Erhöhter Schritt `ZoomIN-Setup.exe --apply-update`: ZoomIN beenden, installieren, „Apps“-Eintrag,
      Startmenü (alle Benutzer); Desktop-Verknüpfung als Benutzer
- [x] Alte Inno-Installation erkennen und still entfernen (`LegacyInstall`)
- [x] Einstellungen des Wizards als angemeldeter Benutzer schreiben (`SetupChoices`)
- [x] Installationsschritt als Liste mit Haken (`InstallChecklist` in Core, getestet): alte Installation →
      Download + Prüfung → Program Files (Windows-Abfrage) → Benutzereinstellungen; jeder Schritt mit
      Zustand für Screenreader, laufender Schritt als Live-Region. Willkommenstext nennt Program Files,
      UIAccess und „Apps“-Eintrag (SignPath-Bedingung). Sichttest offen (braucht einen Release)

**Geprüft 2026-11-03 (lokal):** Wizard-Fenster (dunkle Titelleiste, Sprache, Versionsschritt,
Vergrößerung), Single-File-Setup startet; `ZoomIN-Setup.exe --apply-update` installiert
`1.0.0-dev.102` (laufendes ZoomIN beendet, `.100` aufgeräumt, `.101` behalten, „Apps“-Eintrag,
Startmenü); Deinstallation mit „Einstellungen behalten“ entfernt Ordner, „Apps“, Startmenü und
Autostart, `settings.json` bleibt. Noch nicht geprüft: der Klickweg durch den Wizard bis zur
Installation (braucht einen Release) und die Benutzer-Optionen (Autostart, Desktop, Einstellungen).
- [x] Deinstallation `ZoomIN.exe --uninstall` mit Frage nach den Einstellungen (Standard-Windows-Dialog)

### App
- [x] Einstellungsseite „Updates“: Kanal, Versionsliste, Prüfung an/aus, Aktualisieren/Zurückgehen,
      Status als Live-Region für Screenreader
- [x] Update-Hinweis im Tray, Sicherung von Einstellungen/Profilen vor einem Downgrade
- [x] Update-Prüfung beim Start + täglich; im Erststart-Assistenten vorausgewählt
- [x] Autostart zeigt auf `current\ZoomIN.exe`, nicht auf einen Versionsordner
- [x] Erststart-Assistent überspringen, wenn das Setup schon eingerichtet hat (`FirstRunCompleted`)
- [ ] Ende-zu-Ende mit echtem Release (Klick in der App) – geht erst, wenn ein signierter Release existiert
- [x] Ohne UIAccess (Entwickler-Build, ZIP): Tray-Meldung je Start, Banner auf der Übersicht, Knopf
      lädt und prüft `ZoomIN-Setup.exe` und startet es (ADR-0017). Sichttest 2026-11-03 mit Dev-Build ✅;
      Knopf meldet „noch keine Version mit Setup“, solange kein Release veröffentlicht ist
- [ ] Setup-Knopf Ende-zu-Ende, sobald ein signierter Release mit `ZoomIN-Setup.exe` existiert

**Geprüft 2026-11-03 (lokal, Zertifikat dieses PCs):** Installation in die neue Struktur,
UIAccess über `current` (Band 2), Update `1.0.0-dev.100` → `.101` per Admin-Schritt (Exit 0,
Link umgesetzt, alte Version behalten), abgelehnte Abfrage ändert nichts, Neustart mit
`--wait-for` übernimmt nach dem Ende der alten Instanz.

### Doku
- [x] README und build-und-tooling.md: neuer Installationsweg
- [ ] Handbuch (de/en): Setup, Updates, Deinstallation
- [ ] M8-Release-Anleitung auf das neue Setup umstellen

## Akzeptanzkriterien
- [ ] Frisches Windows 11 und Windows 10: Setup → UIAccess aktiv (Overlay in Band 2),
      Ton-/Netzwerk-Menü nur vergrößert sichtbar
- [ ] Wizard vollständig per Tastatur und mit NVDA/Sprachausgabe bedienbar; Kontrastdesign und
      200 % Textgröße ohne abgeschnittene Inhalte
- [ ] Update stabil → neuere Version, Downgrade, Kanalwechsel; Einstellungen bleiben erhalten
- [ ] Manipulierter Download (falsche Prüfsumme/Signatur) wird abgelehnt
- [ ] Alte Inno-Installation wird sauber ersetzt; Deinstallation entfernt alles außer
      (auf Wunsch) Einstellungen
- [ ] Tests grün, Zeilenregel und Architekturtests eingehalten

## Offene Fragen / Risiken
- SignPath-Genehmigung kann Wochen dauern; ob develop-Signierungen ohne Handfreigabe gehen, ist offen.
- Download-Größe pro Version ~70 MB; ob Delta-Updates nötig sind, später messen.
- GitHub-API-Limit ohne Token: 60 Anfragen/h pro IP – mit täglicher Prüfung unkritisch.
