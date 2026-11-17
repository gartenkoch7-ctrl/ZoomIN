# ZoomIN

**Kostenlose Open-Source-Bildschirmlupe für Windows** – flüssig wie eine Systemfunktion und
**unsichtbar in Bildschirmaufnahmen und -freigaben**. *[English below](#english).*

[![CI](https://github.com/gartenkoch7-ctrl/ZoomIN/actions/workflows/ci.yml/badge.svg)](https://github.com/gartenkoch7-ctrl/ZoomIN/actions/workflows/ci.yml)
[![Lizenz: MIT](https://img.shields.io/badge/Lizenz-MIT-blue.svg)](LICENSE)

## Funktionen
- **Vollbild, Lupe und angedockter Bereich** – 1× bis 16×, stufenlos und weich animiert
- **Unsichtbar in Bildschirmaufnahmen:** Andere sehen Ihren normalen Desktop, nur Sie den Zoom
- **Auch auf Anmelde- und Sperrbildschirm**, bei Strg+Alt+Entf und Windows-Abfragen nach Administratorrechten
- **Farbfilter:** invertiert (auch „intelligent“: nur helle Flächen, Fotos bleiben original),
  Graustufen, Gelb auf Schwarz und weitere, eigene Farbschemata – auch ohne Zoom
- **Filter pro Fenster:** helle Fenster werden automatisch invertiert, dunkle bleiben, wie sie sind
- **Lesbarkeit:** kräftigere und schärfere Schrift, gut sichtbarer Textcursor
- **Gut sichtbarer Mauszeiger** mit Kontrastrand, Suchhilfe und Fadenkreuz
- **Folgt Tastaturfokus und Textcursor**, mit farbigem Fokusrahmen; ruhiges Bild, das sich nur bewegt,
  wenn es nötig ist
- **Übersicht:** kurz auf 1× mit Rahmen um den Ausschnitt, dann wieder zurück
- **Mehrere Monitore**, jeder mit eigener Zoomstufe; einen Monitor einfrieren, vergrößerte Inseln
  (z. B. Uhr oder Chat) über dem Zoom
- **Profile**, Autostart, Updates aus der App heraus, Deutsch und Englisch
- **Absturzsicher:** Ein Wächterprozess startet ZoomIN neu und räumt auf
- **Ihre Daten bleiben auf dem PC:** Diagnose-Logs liegen lokal und enthalten keine Fenstertitel oder
  Texte. Nur wenn Sie selbst eine Telemetrie-Adresse eintragen (oder die Umgebungsvariable
  `OTEL_EXPORTER_OTLP_ENDPOINT` gesetzt ist), gehen Diagnosedaten dorthin. Die
  Update-Prüfung ist abschaltbar und fragt nur die Versionsliste bei GitHub ab.

## Download und Installation
[Neueste Version](https://github.com/gartenkoch7-ctrl/ZoomIN/releases) – für Windows 10 (Version 2004)
oder neuer, 64 Bit (x64).

- **`ZoomIN-Setup.exe` (empfohlen):** Assistent – Sprache, Kanal und Version wählen, einmal
  Administratorrechte bestätigen. Nur so vergrößert ZoomIN auch über Startmenü, Schnelleinstellungen
  und auf dem Sperrbildschirm. Updates kommen danach aus ZoomIN heraus.
- **Portables ZIP `ZoomIN-<Version>-win-x64.zip`:** entpacken und `ZoomIN.exe` starten, ohne
  Administratorrechte – aber ohne Zoom über Startmenü, Schnelleinstellungen und Sperrbildschirm.
- `ZoomIN-<Version>-win-x64-uiaccess.zip` ist das Paket, das Setup und Updates installieren – nicht
  zum Entpacken gedacht, außerhalb von `C:\Program Files` startet es nicht.

### Signatur und SmartScreen
ZoomIN ist kostenlos und hat kein gekauftes Zertifikat. Setup und ZIP sind deshalb **nicht
signiert**, und Windows SmartScreen warnt beim ersten Start: **„Weitere Informationen“ → „Trotzdem
ausführen“**.

Damit Windows ZoomIN über Startmenü und Sperrbildschirm zeichnen lässt, braucht es eine Signatur. Die
erstellt das Setup **auf Ihrem PC**:
- Es prüft das Paket zuerst mit der SHA-256-Prüfsumme aus `checksums.txt` desselben Releases.
- Es legt ein eigenes Zertifikat „ZoomIN Local Signing“ an, das nur Programme signieren kann und
  keine anderen Zertifikate ausstellen darf, und signiert damit `ZoomIN.exe`.
- Den privaten Schlüssel löscht es sofort wieder – niemand kann damit später etwas anderes signieren.
- Jedes Update macht das neu; die Deinstallation entfernt die Zertifikate.

Prüfsummen von Hand: `Get-FileHash <Datei> -Algorithm SHA256` mit `checksums.txt` vergleichen.
Deinstallieren über Windows-Einstellungen → Apps → ZoomIN.

## Bedienung
| Kürzel | Aktion |
|---|---|
| Strg+Alt+Z | Zoom an/aus |
| Strg+Mausrad · Strg+Alt+Bild↑/↓ | Vergrößern/Verkleinern |
| Strg+Alt+F / L / D | Vollbild / Lupe / Angedockt |
| Strg+Alt+C · Strg+Alt+Umschalt+C | Farbfilter an/aus · nächster Filter |
| Strg+Alt+S | Mauszeiger suchen |
| Strg+Alt+K | Fadenkreuz an/aus |
| Strg+Alt+T | Fokus/Textcursor folgen an/aus |
| Strg+Alt+O | Übersicht |
| Strg+Alt+P | Nächstes Profil |
| Strg+Alt+H · Strg+Alt+N | Monitor einfrieren · Zeiger auf den nächsten Monitor |
| Strg+Alt+I · Strg+Alt+Umschalt+I | Inseln ein/aus · Inseln bearbeiten |

Alle Kürzel sind in den Einstellungen änderbar. Ausführlich: [Benutzerhandbuch](docs/benutzer/handbuch.md).

## Selbst bauen
Voraussetzung: .NET 10 SDK.
```powershell
dotnet build ZoomIN.slnx
dotnet test --solution ZoomIN.slnx
dotnet run --project src/ZoomIN.App
./scripts/publish.ps1 -Version 1.0.0          # portables ZIP
./scripts/publish-setup.ps1 -Version 1.0.0    # artifacts\ZoomIN-Setup.exe
```
Mitmachen: [CONTRIBUTING.md](CONTRIBUTING.md). Architektur und Regeln: [AGENTS.md](AGENTS.md).
Hardware-Erfahrungen bitte über die Issue-Vorlage „Hardware-Bericht“ melden.

## Lizenz
[MIT](LICENSE). ZoomIN ist ein eigenständiges Projekt und steht in keiner Verbindung zu
Dolphin Computer Access oder deren Produkten.

---

## English
**ZoomIN** is a free, open-source screen magnifier for Windows 10 (2004+) and 11 (x64). It stays
**invisible in screen recordings and sharing**, and it also magnifies the sign-in and lock screen,
Ctrl+Alt+Del and administrator prompts.

- Full screen, lens and docked modes (1×–16×), smooth and stepless
- Colour filters including a **smart inversion** (only bright areas, photos stay as they are), custom
  colour schemes, and **filters per window** (bright windows are inverted automatically)
- Bolder, sharper text, a clearly visible text cursor, a large pointer with locator and crosshair
- Follows keyboard focus and the text cursor; overview, frozen monitor and magnified islands
- Multiple monitors, profiles, in-app updates, German and English; a watchdog restarts ZoomIN after a crash
- Diagnostic logs stay on your PC and contain no window titles or text; diagnostic data is only sent if
  you enter a telemetry address yourself (or `OTEL_EXPORTER_OTLP_ENDPOINT` is set). The update check can be turned off and only asks GitHub for
  the version list.

**Download:** [latest release](https://github.com/gartenkoch7-ctrl/ZoomIN/releases) –
`ZoomIN-Setup.exe` (recommended, needs administrator rights once; required for magnifying over the
Start menu and on the lock screen) or the portable `ZoomIN-<version>-win-x64.zip`. ZoomIN has no paid
certificate: setup and ZIP are unsigned and SmartScreen warns ("More info" → "Run anyway"). The setup
checks the package against `checksums.txt` and signs ZoomIN on your PC with its own certificate
(code signing only, no CA); the private key is deleted right after signing. The `…-uiaccess.zip` is the package the setup installs – not for unpacking.
Checksums: `checksums.txt`. Uninstall via Windows Settings → Apps → ZoomIN. See the
[manual](docs/benutzer/manual.md) and the shortcuts above (Strg = Ctrl, Umschalt = Shift, Bild = Page).
