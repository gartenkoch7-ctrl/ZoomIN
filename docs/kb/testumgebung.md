# Testumgebung

Stand 2026-10-30. Grundlage für alle Mess- und Kompatibilitätsaussagen.

## Vorhandene Hardware
| Komponente | Wert |
|---|---|
| GPU | NVIDIA GeForce RTX 3060 Ti (Desktop), Treiber 32.0.16.1088 |
| Monitor | 1× AOC 27G2G4, 1920×1080, aktuell 60 Hz (laut Modell bis 144 Hz ⚠️ prüfen) |
| Betriebssystem | Windows 11 Home, Build 22631 |
| Weitere Geräte | keine |

## Strategie
| Bereich | Wie getestet |
|---|---|
| Bildrate / Latenz | Messung bei **60 Hz und 144 Hz** am echten Monitor |
| Multi-Monitor, gemischte DPI | **Virtuelle Monitore** (Indirect Display Driver) – Eignung in M3 prüfen |
| Shader / Rendering | **WARP** (Software-D3D) offscreen, lokal und in CI |
| Windows 10 | VirtualBox-VM mit Windows 10 2004+ (nur Grundfunktion; Treiber in VMs eingeschränkt) |
| Bildschirmfreigabe | Teams, Zoom/Webex, Discord, Browser-Freigabe (Edge/Chrome) |
| Hybrid-Grafik, Laptop-DPI, echte Zweitmonitore, HDR, ARM64 | **nicht testbar** → in Release-Notes als „ungetestet“ |

Keine Beta-Phase (ADR-0009): Rückmeldungen zu ungetesteter Hardware kommen über die
GitHub-Issue-Vorlage „Hardware-Bericht“.

## Ergebnisse
| Datum | Thema | Ergebnis |
|---|---|---|
| – | Desktop Duplication auf virtuellem Monitor | offen (M3, Treiber noch nicht installiert) |
| 2026-10-30 | Grafiktreiber-Reset bei aktivem Zoom (automatisiert) | ✅ Neuaufbau, kein Prozess-Neustart |
| 2026-10-30 | Auflösung 1920×1080 → 1280×720 → zurück (automatisiert, temporär) | ✅ je ein Neuaufbau, Zoom sichtbar |
| 2026-10-30 | Watchdog: Kill → Neustart, 4. Absturz → Hinweis (automatisiert) | ✅ Neustart ~150 ms |

## Programme, die ein Agent startet (2026-11-05)
- Befehle eines Coding-Agents laufen in einer abgeschotteten Umgebung; alles, was er startet
  (auch `ZoomIN.exe`), erbt sie. Was solche Prozesse unter `%LocalAppData%`/`%AppData%` schreiben,
  sieht der normale Explorer offenbar nicht („Der Pfad ist nicht verfügbar“), Systemordner schon.
  Datei-/Ordnerpfade aus solchen Läufen deshalb nicht als Fehlerbeleg nehmen.
- Die installierte ZoomIN soll der Nutzer selbst starten (Startmenü), nicht der Agent: aus der
  Umgebung gestartet hing eine Instanz beim Start ohne Tray-Symbol und Overlay.

## Overlay von außen prüfen (2026-11-06)
- Das Overlay ist aus Screenshots ausgenommen, sein Fenster lässt sich aber abfragen: Klasse
  `ZoomIN.Overlay` per `FindWindow` suchen, `IsWindowVisible` sagt, ob es zeichnet oder pausiert.
- So lässt sich der Filter pro Fenster an einer laufenden App prüfen, ohne Nutzer-Apps anzufassen:
  eigene weiße/schwarze Testfenster (`TestWindow` aus den Platform-Tests) öffnen und schließen.
  Ergebnis am Dev-Build: nur schwarz → unsichtbar, weiß dazu → nach ≈ 1 s sichtbar, zu → unsichtbar.
- Ob die Pixel wirklich invertiert sind, kann so nicht gesehen werden – das decken die WARP-Shadertests ab.
- Der Dev-Build startet über das Terminal-Panel des Nutzers (außerhalb der Agent-Sandbox).

## KI-Debug: ZoomIN per MCP prüfen (2026-11-11, ADR-0027)
- Die installierte ZoomIN läuft mit UIAccess. Windows (UIPI) blockiert deshalb Klicks und Tasten
  von Computer-Use in ihren Fenstern, auch den Knopf „Schnappschuss in 5 s“.
- Stattdessen: In ZoomIN „Allgemein → Diagnose → KI-Debug erlauben“ einschalten. Dann bietet die
  laufende ZoomIN die Pipe `ZoomIN.AgentDebug.<Sitzung>` an (nur derselbe Benutzer).
- Der Agent startet `ZoomIN.Mcp.exe` als MCP-Server (stdio, `.mcp.json` im Repo). Werkzeuge:
  `get_state` (Zustand als Text), `take_snapshot` (Bilder direkt im Ergebnis, mit `crop` und
  `max_width`) und `run_shortcut` (Zoom, Modus, Filter, Filter für das aktive Fenster).
- Ohne MCP-Client geht es auch von Hand: JSON-RPC-Zeilen auf stdin von `ZoomIN.Mcp.exe` (`scripts/agent-debug.ps1`)
  (`initialize`, `notifications/initialized`, `tools/call`), Bilder kommen als base64-PNG.
- Die Bilder werden über die Pipe geschickt und der Ordner danach gelöscht. Es bleibt kein
  Bildschirminhalt auf der Platte, und die Sandbox-Sicht des Agents auf AppData spielt keine Rolle.
- Tests für den Filter pro Fenster: nur eigene Testfenster nach vorn holen, keine Apps des Nutzers.
- Erfahrungen vom ersten Lauf (2026-11-11):
  - Ein Testfenster aus einem Hintergrundprozess kann sich nicht selbst nach vorn holen
    (Windows-Vordergrundsperre). `toggle_window_filter` traf deshalb das ZoomIN-Fenster. Das
    Testfenster vorher per Computer-Use anklicken und mit `get_state` („Foreground window“) prüfen.
  - Regeln gelten je App + Fensterklasse: Zwei WinForms-Testfenster aus einem Prozess teilen
    sich eine Regel.
  - Tests ändern die echte `%AppData%\ZoomIN\settings.json` (auch der Dev-Build). Vorher sichern
    und danach Regeln, Modus und Zoomstufe zurücksetzen.
