# M10 – Zoom auf Anmeldebildschirm und sicheren Bildschirmen

**Ziel:** ZoomIN vergrößert automatisch auch auf dem Anmeldebildschirm nach dem Start, dem
Sperrbildschirm, Strg+Alt+Entf (inkl. „Kennwort ändern“) und UAC-Abfragen – mit vollem
Funktionsumfang und ohne neue Sicherheitslücke.
**Demo:** Zoom an, Filter an → Win+L → Sperrbildschirm erscheint in ≤ 1 s gleich vergrößert und
gefiltert → Kennwort eingeben → ZoomIN läuft normal weiter. Dasselbe nach Neustart des PCs, bei
Strg+Alt+Entf → „Kennwort ändern“ und bei einer UAC-Abfrage.
**Voraussetzungen:** M9 (Installation in `Program Files`, `current`-Link, `--apply-update`).
→ [ADR-0016](../adr/0016-zoom-auf-sicheren-bildschirmen.md)

## Entscheidungen (Grilling 2026-11-03)
| Thema | Entscheidung |
|---|---|
| Weg | Windows-AT-Registrierung (AtBroker), kein eigener Dienst |
| Umfang | Anmeldung nach Start, Sperre, Strg+Alt+Entf/Kennwort ändern, UAC |
| Prozess | `ZoomIN.exe --secure-desktop`: volle Engine, kein WinUI/Tray/Einstellungsfenster/Updates |
| Einstellungen | Hybrid: Sitzungsbenutzer lesen (nur lesen, streng geprüft); Kopie in `%ProgramData%\ZoomIN\secure` für den Start ohne Anmeldung |
| Zustand | Live-Zustand beim Wechsel übernehmen, Einbahn (nichts zurückschreiben) |
| Schalter | Setup (vorausgewählt) + App-Schalter mit Admin-Abfrage; Updates schalten ein, „aus“ bleibt aus |
| Tastatur | Hook bleibt, liefert nur „Zeichentaste“ und „Strg allein“ |
| Startzeit | ≤ 1 s bis zum vergrößerten Bild |
| Notbremse | Strg+Alt+Z, Beenden-Kürzel (nur sicherer Modus), max. 3 Neustarts, nie Schwarzbild |
| Plan B | Minimaler Startdienst – nur mit neuem ADR und Rücksprache |
| Tests | Dev-Schalter, Logs in `%ProgramData%\ZoomIN\logs`, Checkliste auf dem echten Anmeldebildschirm |
| Portables ZIP | nicht unterstützt |

## Aufgaben
### 1 · Spike (Risiko zuerst)
- [ ] AT-Registrierung von Hand (Skript `scripts/spike-secure-desktop.ps1`, Admin): welche Werte
      unter `ATs\ZoomIN` und `Configuration` (HKLM für Anmeldung, HKCU für Sperre/UAC?) nötig sind
- [ ] Minimaler Testlauf als SYSTEM auf `Winlogon`: Log mit Benutzer, Sitzung, Desktop, Startzeit
- [ ] Desktop Duplication auf dem sicheren Desktop: liefert Bilder? `WDA_EXCLUDEFROMCAPTURE` wirkt?
- [ ] Z-Reihenfolge: liegt das Overlay über LogonUI, Sperrbildschirm und UAC-Dialog? Braucht es
      UIAccess bzw. eine Signatur, auch ohne SignPath?
- [ ] Mauszeiger (`MagShowSystemCursor`), RegisterHotKey, LL-Hooks, UI Automation dort
- [ ] Ergebnis in `docs/kb/sichere-desktops.md`; bei Fehlschlag → Plan B mit Rücksprache

### 2 · Engine ohne WinUI
- [ ] Gemeinsame Zusammenstellung der Engine aus `ServiceRegistration`/`AppShell` herauslösen
      (Zoom, Effekte, Tracking, Hotkeys, Hooks, Render-Threads), WinUI-frei
- [ ] Startpfad `--secure-desktop` in `Program.cs`: eigener Einzelinstanz-Name, Win32-Nachrichten-
      schleife statt `Application.Start`, keine Updates/Autostart/Tray
- [ ] Dev-Schalter: sicherer Modus auf dem normalen Desktop startbar (schnelle Tests)
- [ ] Startzeit messen und loggen (Prozessstart → erstes vergrößertes Bild)

### 3 · Einstellungen & Zustand
- [ ] Core: `SecureSettingsPolicy` – wählt Quelle (Sitzungsbenutzer / Maschinenkopie / Standard),
      begrenzt jeden Wert auf seinen Bereich (Unit-Tests mit kaputten und bösartigen Dateien)
- [ ] Platform: Benutzerordner des Sitzungsbenutzers ermitteln (Sitzungs-Token), Datei sicher lesen
      (Größenlimit, keine Reparse-Points, Fehler → Kopie bzw. Standard)
- [ ] Platform: Maschinenkopie in `%ProgramData%\ZoomIN\secure` schreiben, Ordner-ACL nur SYSTEM/Admins
- [ ] Profile mitlesen, damit Strg+Alt+P auch dort funktioniert
- [ ] Benutzerinstanz: bei Sperre/Desktopwechsel (`WTS_SESSION_LOCK` u. a.) Zustand sofort speichern

### 4 · Registrierung, Setup & Updates
- [ ] Platform: `SecureDesktopRegistration` (registrieren/entfernen/abfragen, Merker „vom Nutzer aus“)
- [ ] Setup: Kontrollkästchen „Auch auf dem Anmeldebildschirm verwenden“ (vorausgewählt)
- [ ] `--apply-update`: registrieren, sofern nicht ausgeschaltet; Deinstallation entfernt alles
- [ ] App, Seite „Allgemein“: Schalter mit Admin-Abfrage (erhöhter Schritt wie bei Updates), DE/EN

### 5 · Sicherheit & Robustheit
- [ ] Tastatur-Hook: Schnittstelle liefert nachweislich nur zwei Ereignisse (Test)
- [ ] Beenden-Kürzel nur im sicheren Modus (gegen `HotkeyRules`/ADR-0008 prüfen)
- [ ] Wächter im sicheren Modus: Neustart ohne Shell, max. 3×, danach aus bis Systemstart
- [ ] Overlay zeigt nichts, solange kein Bild erfasst wurde (kein Schwarzbild)
- [ ] Logs nach `%ProgramData%\ZoomIN\logs`, keine Eingabedaten
- [ ] `/security-review` des Branches vor dem Merge

### 6 · Test auf dem echten PC (Nutzer, Checkliste)
- [ ] Neustart → Anmeldebildschirm vergrößert (Einstellungen der Kopie)
- [ ] Win+L → Sperre vergrößert, gleicher Zustand wie vorher, ≤ 1 s; nach Entsperren läuft die App weiter
- [ ] Strg+Alt+Entf → Menü und „Kennwort ändern“ vergrößert, Tippen ins Kennwortfeld mit Caret-Tracking
- [ ] UAC-Abfrage vergrößert
- [ ] Alle Modi, Filter pro Monitor, Mauszeiger-Optionen, Tastenkürzel, Profilwechsel
- [ ] Zweiter Monitor; Beenden-Kürzel; Schalter aus → nichts startet mehr
- [ ] Unsichtbar bei Übertragung bleibt gewahrt (Overlay-Fenster mit `WDA_EXCLUDEFROMCAPTURE`)

## Akzeptanzkriterien
- Alle Punkte aus Abschnitt 6 bestanden, Startzeit ≤ 1 s in den Logs.
- Kein Tastencode, kein Text aus UI Automation in Logs oder Dateien.
- DoD aus `AGENTS.md`: Build ohne Warnungen, Tests grün, `check-file-length.ps1`, Architekturtests,
  KB aktualisiert (`sichere-desktops.md`, `rendering-pipeline.md`, `capture-ausschluss.md`).

## Offene Fragen / Risiken
- AtBroker-Werte nur halb dokumentiert → Spike.
- Z-Reihenfolge über LogonUI/UAC eventuell nur mit UIAccess; als SYSTEM ungeklärt.
- Laufen die WinUI-freien Teile wirklich ohne Windows App SDK-Initialisierung?
- Ordnerumleitung/Netzwerkprofile: SYSTEM kann den Benutzerordner evtl. nicht lesen → Kopie.
- Startzeit von .NET + D3D auf langsamen PCs.
