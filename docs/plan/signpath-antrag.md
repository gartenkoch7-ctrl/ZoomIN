# SignPath-Antrag – Vorbereitung (M9)

Ziel: kostenloses Code-Signing über die **SignPath Foundation**, damit `ZoomIN.exe` (UIAccess) und
`ZoomIN-Setup.exe` auf jedem PC starten und SmartScreen nicht warnt (ADR-0014).
Stand der Bedingungen: 2026-11-03, laut [signpath.org/terms](https://signpath.org/terms) –
die Seite ist als **Entwurf** markiert, vor dem Absenden noch einmal lesen.

**Abschicken musst du selbst** unter [signpath.org/apply](https://signpath.org/apply). Das Formular
lädt per Skript; seine genauen Felder konnte ich nicht auslesen. Die Antworten unten decken ab,
was SignPath laut Bedingungen prüft.

---

## 1. Bedingungen gegen ZoomIN geprüft

| Bedingung (SignPath) | ZoomIN heute | Zu tun |
|---|---|---|
| OSI-Lizenz für alle Teile, kein Dual-Licensing | MIT ✅ | – |
| Keine proprietären Teile (System-Bibliotheken erlaubt) | Vortice, CommunityToolkit, MS.Extensions: MIT. Windows App SDK + .NET-Laufzeit: Microsoft-Plattform | Im Antrag als Plattform-/System-Bibliotheken nennen; signiert werden nur **unsere** Dateien |
| Aktiv gepflegt | ✅ laufende Commits | – |
| **Schon veröffentlicht** in der Form, die signiert wird | ❌ **kein Release** | **Blocker**, siehe Abschnitt 2 |
| Funktion auf der Download-Seite beschrieben | README ✅, Release-Notes vorhanden | Release-Seite mit Beschreibung füllen |
| Nur eigene Projekte/Binärdateien signieren | ✅ eigenes Repo, eigene Build-Skripte | Nur ZoomIN-Dateien signieren, keine Microsoft-DLLs |
| Keine Hacking-Tools | Lupe mit Hooks und UIAccess – **legitime Hilfstechnologie** | Im Antrag erklären (Text unten) |
| Datenschutz: Daten an unbestimmte Systeme nur mit Erklärung + abschaltbar | Nur Abfrage der Release-Liste bei GitHub, im Setup abwählbar ✅ | Datenschutz-Erklärung veröffentlichen (Text unten) |
| Systemänderungen ankündigen | Setup nennt Admin-Rechte, Program Files, UIAccess und den „Apps“-Eintrag (2026-11-03); Autostart und Anmeldebildschirm sind Optionen; Schrittliste zeigt jeden Schritt ✅ | – |
| Deinstallation vorhanden | ✅ „Apps“ → Deinstallieren, getestet | – |
| **MFA** für Repo und SignPath bei allen Beteiligten | ? | **Du:** 2FA bei GitHub einschalten, SignPath-Konto mit MFA |
| Rollen: Authors, Reviewers, Approvers | Ein Maintainer | Alle drei Rollen = `gartenkoch7-ctrl` |
| Abschnitt **„Code signing policy“** auf Startseite und Download-Seiten | ❌ | Text unten ins README und in die Release-Notes |
| Metadaten: Produktname = Projektname, gleiche Version in allen Dateien | `Product` = „ZoomIN“, Version per `-p:Version` ✅ | – |
| Builds nur auf GitHub-gehosteten Runnern | `windows-latest`/`ubuntu-latest` ✅ | – |
| Jede Signierung wird von Hand freigegeben | Workflow wartet (`wait-for-completion`) ✅ | Für develop-Builds heißt das: **jeder Push = eine Freigabe** |

## 2. Blocker: Es gibt noch keinen Release

SignPath verlangt, dass das Projekt **schon veröffentlicht** ist. Unsere Workflows veröffentlichen
aber bewusst erst mit Signatur (ADR-0014, Q17). Ausweg, ohne die Entscheidung umzuwerfen:

- **Einmaliger, von Hand erstellter Vorab-Release `v1.0.0-rc1`** mit dem **portablen ZIP** (läuft
  unsigniert, ohne UIAccess) und klarem Hinweis „unsigniert, Signatur beantragt“.
- Das unsignierte Setup und das UIAccess-Paket kommen **nicht** in diesen Release – sie würden auf
  fremden PCs nicht starten.

Diese Entscheidung ist deine; sie steht hier nur als Vorschlag.

## 3. Antworten für den Antrag (Englisch, zum Kopieren)

**Project name:** ZoomIN

**Repository:** https://github.com/gartenkoch7-ctrl/ZoomIN

**License:** MIT

**Short description:**
> ZoomIN is a free, open-source screen magnifier for Windows 10/11 for people with low vision:
> smooth full-screen, lens and docked zoom up to 16×, colour filters, a large pointer, and focus
> and text-cursor tracking. The magnified view is excluded from screen sharing (Teams, Zoom, OBS).

**Why code signing:**
> To show the zoom above the Start menu and quick settings, Windows only grants UIAccess (the
> accessibility privilege also used by the built-in Magnifier) to executables that are signed with
> a trusted certificate and installed in Program Files. Without a trusted signature ZoomIN cannot
> work correctly for its users. Signing also lets the in-app updater verify packages before
> installing them.

**About hooks and UIAccess (no hacking tool):**
> ZoomIN uses low-level mouse and keyboard hooks that only observe input (every event is passed on
> unchanged): for zooming with Ctrl+mouse wheel, the pointer locator and hiding the pointer while
> typing. Global shortcuts use RegisterHotKey, and UI Automation follows focus and the text cursor –
> standard techniques of assistive technology. It does not bypass security features: UIAccess is requested through the application
> manifest and granted by Windows only to the signed build in Program Files.

**What will be signed:** `ZoomIN.exe` and the ZoomIN assemblies of the installed build, and the
setup `ZoomIN-Setup.exe`. Built by GitHub Actions on GitHub-hosted runners from tags (releases) and
pushes to `develop` (experimental pre-releases). Microsoft runtime files are not signed by us.

**Third-party components:** Vortice.Windows (MIT), CommunityToolkit.Mvvm (MIT),
Microsoft.Extensions.DependencyInjection (MIT); .NET runtime and Windows App SDK as platform
(system) libraries.

**Team / roles:** Philipp (GitHub `gartenkoch7-ctrl`) – author, reviewer and approver.

**Data / privacy:** ZoomIN collects no telemetry. If the user keeps the update check enabled
(optional in the setup and settings), ZoomIN requests the public release list from the GitHub API
at start-up and once a day. Nothing else is transmitted.

## 4. Texte für README und Release-Seiten

Erst **nach** der Zusage einfügen – vorher wäre die Zeile über die Signatur falsch.

```markdown
## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/),
certificate by [SignPath Foundation](https://signpath.org/).

- Committers and reviewers: [gartenkoch7-ctrl](https://github.com/gartenkoch7-ctrl)
- Approvers: [gartenkoch7-ctrl](https://github.com/gartenkoch7-ctrl)

Only release and pre-release builds made by GitHub Actions from this repository are signed.

### Privacy policy

This program will not transfer any information to other networked systems unless specifically
requested by the user or the person installing or operating it. The optional update check
(on by default, can be turned off in the setup and in Settings → Updates) only requests the
public list of releases from the GitHub API (api.github.com).
```

## 5. Nach der Zusage – was ich dann umsetze

1. GitHub-App `signpath` fürs Repo installieren (du), in SignPath das Projekt mit dem
   Trusted Build System „GitHub.com“ verknüpfen, Signing Policy mit Freigabe anlegen.
2. In GitHub: Secret `SIGNPATH_API_TOKEN`, Variablen `SIGNPATH_ORGANIZATION_ID`,
   `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG` (du; Werte aus SignPath).
3. Artifact Configuration in SignPath (du, als Text einfügen). `build-release.yml` lädt **eine**
   ZIP-Datei mit `uiaccess/` und `setup/` hoch. Vor dem Einfügen mit der SignPath-Doku
   („Artifact Configuration“) abgleichen – Syntax aus dem Gedächtnis, nicht live geprüft:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <zip-file>
       <directory path="uiaccess">
         <pe-file path="ZoomIN.exe" product-name="ZoomIN">
           <authenticode-sign />
         </pe-file>
         <!-- KI-Debug (ADR-0027): app host without UIAccess for MCP clients -->
         <pe-file path="ZoomIN.Mcp.exe" product-name="ZoomIN">
           <authenticode-sign />
         </pe-file>
         <pe-file-set>
           <include path="ZoomIN.dll" product-name="ZoomIN" />
           <include path="ZoomIN.*.dll" product-name="ZoomIN" />
           <for-each>
             <authenticode-sign />
           </for-each>
         </pe-file-set>
       </directory>
       <directory path="setup">
         <pe-file path="ZoomIN-Setup.exe" product-name="ZoomIN">
           <authenticode-sign />
         </pe-file>
       </directory>
     </zip-file>
   </artifact-configuration>
   ```
4. ✅ Erledigt (2026-11-03): Workflow signiert `ZoomIN-Setup.exe` mit, Action steht auf `@v3`;
   `package-release.ps1` bricht ab, wenn das Setup nicht gültig signiert ist
   (lokal: `-AllowUnsignedSetup`).
5. **`TrustedPublishers` anpassen:** Alle OSS-Projekte erhalten denselben Herausgeber
   „SignPath Foundation“. Der Name allein beweist also nicht, dass eine Datei von ZoomIN stammt.
   Zusätzlich prüfen: Produktname in den Datei-Metadaten = „ZoomIN“ (SignPath erzwingt ihn) – dazu
   bleibt die SHA-256 aus unserem eigenen Release. Danach Sperrlisten-Prüfung einschalten.
6. README- und Release-Texte aus Abschnitt 4 einfügen, ersten signierten Release `v1.0.0` taggen.
7. Danach funktioniert der Knopf „Signierte Version installieren“ in der App (ADR-0017) ohne
   Codeänderung: Er findet `ZoomIN-Setup.exe` im Release, prüft Prüfsumme und Signatur.

## 6. Deine Schritte, in dieser Reihenfolge
1. 2FA bei GitHub einschalten (Einstellungen → Password and authentication).
2. Entscheiden, ob der Vorab-Release `v1.0.0-rc1` mit portablem ZIP (Abschnitt 2) veröffentlicht
   wird – ich kann ihn vorbereiten, veröffentlichen erst nach deinem Okay.
3. Antrag auf [signpath.org/apply](https://signpath.org/apply) mit den Texten aus Abschnitt 3.
4. Nach der Zusage: Schritte 1–3 aus Abschnitt 5 (GitHub-App, Secrets, Artifact Configuration);
   den Rest (5–6) übernehme ich.

## Quellen
- [SignPath Foundation – Bedingungen](https://signpath.org/terms)
- [SignPath-Doku: GitHub als Trusted Build System](https://docs.signpath.io/trusted-build-systems/github)
- [Beispiel einer „Code signing policy“ (pandoc)](https://pandoc.org/code-signing-policy.html)
- [Herausgeber „SignPath Foundation“ (Super Productivity)](https://super-productivity.com/code-signing/)
