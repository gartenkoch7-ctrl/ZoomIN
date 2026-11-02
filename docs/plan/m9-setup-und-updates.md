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
- [ ] SignPath-Antrag stellen (OSS); klären, ob develop-Signierungen ohne Handfreigabe gehen
- [x] Core (`ZoomIN.Core.Updates`): `AppVersion` (SemVer-Reihenfolge inkl. `-rc`/`-dev.N`),
      `ReleaseChannel`, `ReleaseInfo`, `ReleaseCatalog` (Kanalfilter, Update, Downgrade-Erkennung),
      `ReleaseAssets`, `ReleaseFeeds` – mit Unit-Tests
- [x] Platform: `GitHubReleaseFeed` + testbarer `GitHubReleaseParser` (Liste statt nur `latest`)
- [x] Platform: `HttpPackageDownloader` (Fortschritt, SHA-256 beim Streamen, nur passende Datei
      bleibt liegen) und `AuthenticodeVerifier` (WinVerifyTrust + festgenagelter Herausgeber aus
      `TrustedPublishers`); Core: `ChecksumList`, `PackageCheck`
- [ ] SignPath-Zertifikatsname in `TrustedPublishers` eintragen, Sperrlisten-Prüfung
      (`WTD_REVOKE_WHOLECHAIN`) dann einschalten – das lokale Zertifikat hat keine Sperrliste
- [ ] App: alten `IUpdateChecker`/`GitHubUpdateChecker` durch `IReleaseFeed` ersetzen

**Pflicht-Assets je Release** (sonst ignoriert der Feed den Release):
`ZoomIN-<version>-win-x64-uiaccess.zip` (signierter UIAccess-Build) und `checksums.txt`
(SHA-256 aller Assets). Kanal folgt der Version: mit Vorabkennung → Experimentell.

### CI
- [ ] `release.yml`: UIAccess-Build, `checksums.txt`, SignPath-Signierung, Setup als Asset
- [ ] Neuer Workflow für `develop`: Version `x.y.z-dev.N`, Vorabversion, signiert
- [ ] Inno-Setup-Schritt und `installer/ZoomIN.iss` entfernen

### Setup-Programm (`ZoomIN.Setup`)
- [ ] Projekt anlegen, Schichtregeln in den Architekturtests ergänzen
- [ ] Wizard-Seiten (siehe Schritte oben), Sprache umschaltbar, eigene Vergrößerung (Strg+Plus/Mausrad)
- [ ] Erhöhter Hilfsprozess: beenden, kopieren, Startmenü/Desktop, Deinstallationseintrag in „Apps“
- [ ] Alte Inno-Installation erkennen und entfernen
- [ ] Einstellungen des Wizards in `%AppData%\ZoomIN\` schreiben (als angemeldeter Benutzer)
- [ ] Deinstallation mit Frage nach den Einstellungen

### App
- [ ] Einstellungsseite „Updates“: Kanal, Versionsliste, Prüfung an/aus, „Jetzt installieren“
- [ ] Update-Hinweis (Tray + Einstellungen), Sicherung von Einstellungen/Profilen vor Wechsel
- [ ] Update-Prüfung beim Start + täglich; Erststart-Assistent überspringen, wenn eingerichtet

### Doku
- [ ] `docs/kb/build-und-tooling.md`, Handbuch (de/en), README: neuer Installationsweg
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
