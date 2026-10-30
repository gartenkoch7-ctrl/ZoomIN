# Produkt & Scope

## Vision
Eine **kostenlose, quelloffene** Bildschirmlupe für sehbehinderte Menschen unter Windows,
die sich so flüssig anfühlt wie eine native Systemfunktion und bei Bildschirmübertragungen
unsichtbar bleibt.

## Zielgruppe
- Menschen mit Sehbehinderung, die heute kommerzielle Lupen (z. B. SuperNova, ZoomText)
  nutzen oder sich diese nicht leisten können.
- Berufstätige, die in Meetings ihren Bildschirm teilen, ohne dass andere den Zoom sehen.

## Qualitätsziele (in dieser Reihenfolge)
1. **Flüssigkeit:** Bildrate = Monitor-Bildwiederholrate, kein Ruckeln, kein Tearing.
2. **Latenz:** Zoom folgt Maus/Fokus innerhalb von 1–2 Frames.
3. **Privatsphäre:** Zoom nie in Bildschirmaufnahmen/-übertragungen sichtbar.
4. **Bedienbarkeit:** komplett per Tastatur steuerbar, gut lesbare, kontrastreiche UI.
5. **Ressourcen:** geringe CPU-/GPU-Last, auch auf Onboard-Grafik.

## MVP (Release 1.0)
| Feature | Beschreibung |
|---|---|
| Vollbild-Zoom | Ganzer Monitor vergrößert, 1×–16× (später bis 64×), stufenlos animiert |
| Lupe (Lens) | Frei verschiebbares/größenveränderbares Lupenfenster um den Zeiger |
| Angedockt (Split) | Fester Bildschirmbereich (oben/unten/links/rechts) zeigt die Vergrößerung |
| Farbfilter | Invertieren, Graustufen, Farbschemata, Kontrast, Helligkeit |
| Hervorhebung | Großer/farbiger Mauszeiger, Fokus- und Caret-Markierung |
| Tracking | Maus, Tastaturfokus, Textcursor (UI Automation) |
| Multi-Monitor | Jeder Monitor einzeln, unterschiedliche DPI |
| Unsichtbar bei Übertragung | `WDA_EXCLUDEFROMCAPTURE` auf allen Overlays |
| Settings & Profile | JSON in `%AppData%\ZoomIN\`, globale Hotkeys |
| Tray & Einstellungen | WinUI-3-Fenster, Tray-Icon, Autostart |

## Nicht im MVP (vorbereitet, später)
- Sprachausgabe (TTS) unter Maus/Fokus → Projekt `ZoomIN.Speech`
- Screenreader, Braille
- Line-View / Textumbruch-Ansicht
- Input-Transform für Kanten-Tracking (UIAccess, signierte EXE) – siehe ADR-0003
- Installer mit Code-Signing (z. B. SignPath für OSS)

## Plattform
Windows 10 2004 (Build 19041) und Windows 11, x64 und ARM64.
