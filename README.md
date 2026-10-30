# ZoomIN

**Kostenlose Open-Source-Bildschirmlupe für Windows** – flüssig wie eine Systemfunktion und
**unsichtbar bei Bildschirmfreigabe** (Teams, Zoom, Discord, Browser). *English below.*

[![CI](https://github.com/OWNER/ZoomIN/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/ZoomIN/actions/workflows/ci.yml)

## Funktionen
- **Vollbild, Lupe, angedockter Bereich** – 1× bis 16×, stufenlos und weich animiert
- **Unsichtbar in Bildschirmaufnahmen**: Andere sehen Ihren normalen Desktop, nur Sie den Zoom
- **Farbfilter**: invertiert (auch farbtonerhaltend), Graustufen, Gelb auf Schwarz, Schwarz auf Gelb, Weiß auf Blau – auch ohne Zoom
- **Gut sichtbarer Mauszeiger** mit Kontrastrand und Suchhilfe
- **Folgt Tastaturfokus und Textcursor**, mit farbigem Fokusrahmen
- **Mehrere Monitore**, jeder mit eigener Zoomstufe
- **Profile**, Autostart, Deutsch und Englisch
- Absturzsicher: ein Wächterprozess startet ZoomIN neu und räumt auf
- **Keine Telemetrie.** Die Update-Prüfung ist freiwillig und fragt nur die Versionsnummer ab.

## Download
[Neueste Version](https://github.com/OWNER/ZoomIN/releases) – Installer (`ZoomIN-…-setup.exe`, ohne
Admin-Rechte) oder ZIP zum Entpacken. Windows 10 2004 oder neuer, 64 Bit (x64).

> Windows SmartScreen kann beim ersten Start warnen, weil ZoomIN noch nicht digital signiert ist:
> „Weitere Informationen“ → „Trotzdem ausführen“.

## Bedienung
| Kürzel | Aktion |
|---|---|
| Strg+Alt+Z | Zoom an/aus |
| Strg+Mausrad · Strg+Alt+Bild↑/↓ | Vergrößern/Verkleinern |
| Strg+Alt+F / L / D | Vollbild / Lupe / Angedockt |
| Strg+Alt+C · Strg+Alt+Umschalt+C | Farbfilter an/aus · nächster Filter |
| Strg+Alt+S | Mauszeiger suchen |
| Strg+Alt+T | Fokus/Textcursor folgen an/aus |
| Strg+Alt+P | Nächstes Profil |

Alle Kürzel sind in den Einstellungen änderbar. Ausführlich: [Benutzerhandbuch](docs/benutzer/handbuch.md).

## Selbst bauen
Voraussetzung: .NET 10 SDK.
```powershell
dotnet build ZoomIN.slnx
dotnet test --solution ZoomIN.slnx
dotnet run --project src/ZoomIN.App
./scripts/publish.ps1 -Version 1.0.0-rc1   # ZIP (+ Installer, wenn Inno Setup installiert ist)
```
Mitmachen: [CONTRIBUTING.md](CONTRIBUTING.md). Architektur und Regeln: [AGENTS.md](AGENTS.md).

## Lizenz
[MIT](LICENSE). ZoomIN ist ein eigenständiges Projekt und steht in keiner Verbindung zu
Dolphin Computer Access oder deren Produkten.

---

## English
**ZoomIN** is a free, open-source screen magnifier for Windows 10 (2004+) and 11 (x64):
full screen, lens and docked modes (1×–16×), colour filters, a large pointer with locator,
focus and text-cursor tracking, multiple monitors and profiles – and it stays **invisible in
screen sharing and recordings**. No telemetry. See the [manual](docs/benutzer/manual.md) and
the shortcuts above (Strg = Ctrl, Umschalt = Shift, Bild = Page).
