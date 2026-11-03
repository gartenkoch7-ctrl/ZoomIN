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
| Einstellungen | Hybrid: Sitzungsbenutzer lesen (nur lesen, streng geprüft); Kopie in SYSTEMs Profil (`…\systemprofile\AppData\Local\ZoomIN\secure`) für den Start ohne Anmeldung |
| Zustand | Live-Zustand beim Wechsel übernehmen, Einbahn (nichts zurückschreiben) |
| Schalter | Setup (vorausgewählt) + App-Schalter mit Admin-Abfrage; Updates schalten ein, „aus“ bleibt aus |
| Tastatur | Hook bleibt, liefert nur „Zeichentaste“ und „Strg allein“ |
| Startzeit | ≤ 1 s bis zum vergrößerten Bild |
| Notbremse | Strg+Alt+Z, Beenden-Kürzel (nur sicherer Modus), max. 3 Neustarts, nie Schwarzbild |
| Plan B | Minimaler Startdienst – nur mit neuem ADR und Rücksprache |
| Tests | Dev-Schalter, Logs in SYSTEMs Profil (`…\ZoomIN\logs`), Checkliste auf dem echten Anmeldebildschirm |
| Portables ZIP | nicht unterstützt |

## Aufgaben
### 1 · Spike (Risiko zuerst)
- [x] Microsoft-Doku zur AT-Registrierung ausgewertet → `docs/kb/sichere-desktops.md`
- [ ] Registrierung von Hand (Nutzer, Admin) laut KB mit `current\ZoomIN.exe --secure-desktop`:
      welche Werte unter `ATs\…` und `Configuration` (HKLM für Anmeldung, HKCU für Sperre/UAC) nötig
      sind. Ein eigenes Prüfskript wird nicht gebaut – getestet wird mit ZoomIN selbst und dessen Log
- [ ] Testlauf als SYSTEM auf `Winlogon`: Log zeigt `SYSTEM: True` und die Startzeit
- [ ] Desktop Duplication auf dem sicheren Desktop: liefert Bilder? `WDA_EXCLUDEFROMCAPTURE` wirkt?
- [ ] Z-Reihenfolge: liegt das Overlay über LogonUI, Sperrbildschirm und UAC-Dialog? Braucht es
      UIAccess bzw. eine Signatur, auch ohne SignPath?
- [ ] Mauszeiger (`MagShowSystemCursor`), RegisterHotKey, LL-Hooks, UI Automation dort
- [ ] Ergebnis in `docs/kb/sichere-desktops.md`; bei Fehlschlag → Plan B mit Rücksprache

### 2 · Engine ohne WinUI
- [x] Engine-Zusammenstellung WinUI-frei: `EngineRegistration.AddEngine` (geteilt von Tray-App und
      sicherem Modus), `ServiceRegistration.BuildSecureDesktop`
- [x] Startpfad `--secure-desktop` (`SecureDesktopHost`): eigener Einzelinstanz-Name, kein
      `Application.Start`, keine Updates/Autostart/Tray; Hauptthread wartet auf das Beenden-Kürzel
- [x] Dev-Schalter: derselbe Schalter als normaler Benutzer läuft auf dem normalen Desktop
      (Rauchtest 2026-11-03: Engine nach 646 ms bereit)
- [x] Startzeit bis zum **ersten vergrößerten Bild** loggen (`Secure desktop: first magnified image
      after … ms`, nur wenn der Zoom beim Start an ist); Messung auf dem echten Anmeldebildschirm offen

### 3 · Einstellungen & Zustand
- [x] Core: `SecureSettingsSanitizer` begrenzt jeden Wert (NaN, unbekannte Enums, zu große
      Wörterbücher/Texte, Kürzel, die Tippen blockieren) – Unit-Tests
- [x] Platform: `SessionUserFolders` (Sitzungs-Token → Roaming-AppData des Benutzers),
      `GuardedFileReader` (echter Pfad = erwarteter Pfad, kein Hardlink, ≤ 256 KB) – Tests mit
      Junction und Hardlink
- [x] Platform: `SecureSettingsStore` – Benutzer → Kopie → Standard; Kopie in SYSTEMs Profil;
      `Save` schreibt nie
- [x] `SecureProfileStore`: Profile nur lesen, damit Strg+Alt+P auch dort funktioniert
- [x] Neues Feld `ZoomOn`; der sichere Modus startet damit gezoomt
- [x] Benutzerinstanz: Zoom an/aus wird sofort gespeichert (ohne die 1-s-Verzögerung), damit
      Win+L direkt nach Strg+Alt+Z stimmt. Zoomstufen bleiben verzögert – wer per Mausrad zoomt und
      binnen 1 s sperrt, sieht auf dem Sperrbildschirm die vorige Stufe (bewusst, kein
      Sitzungs-Hook nötig; gilt so auch für Strg+Alt+Entf und UAC, die kein Sperr-Ereignis senden)
- [ ] Erkenntnis: In einer MSIX-Umgebung (z. B. Shell aus einer Paket-App) leitet Windows AppData
      um; der Pfadvergleich weist das ab → Standardwerte. Für SYSTEM nicht relevant

### 4 · Registrierung, Setup & Updates
- [x] Platform: `SecureDesktopRegistration` (HKLM `ATs\ZoomIN` + `Configuration`, Merker
      `HKLM\SOFTWARE\ZoomIN\SecureDesktop`); andere Einträge (z. B. `magnifierpane`) bleiben – Tests
- [x] Windows startet `current\ZoomIN.exe` **ohne Argumente**; als SYSTEM ist ZoomIN immer der
      sichere Modus, egal mit welchen Argumenten (SYSTEM bekommt nie WinUI, Dialoge oder Updates)
- [x] Setup: Kontrollkästchen „Auch auf dem Anmeldebildschirm …“ (vorausgewählt), als
      `--secure-desktop on|off` an den erhöhten Schritt
- [x] `--apply-update`: registriert, sofern nicht ausgeschaltet; Deinstallation entfernt
      Registrierung, Merker und den Ordner in SYSTEMs Profil
- [x] App, Seite „Allgemein“: Schalter „Auf dem Anmeldebildschirm verwenden“, Admin-Abfrage über
      `--secure-desktop-setting on|off`; bei „Nein“ springt er zurück; aus bei Dev-Build/ZIP. DE/EN
- [ ] HKCU-`Configuration` wird bewusst **nicht** geschrieben (würde ZoomIN nach der Anmeldung
      zusätzlich im Job starten). Spike klärt, ob Sperre/UAC ohne sie starten (laut Doku: wenn
      `ATExe` schon läuft)

### 5 · Sicherheit & Robustheit
- [x] Tastatur-Hook: Schnittstelle liefert nachweislich nur zwei Ereignisse (`InputActivityPrivacyTests`)
- [x] Beenden-Kürzel Strg+Alt+Umschalt+Z nur im sicheren Modus (Test gegen AltGr-Regel)
- [x] Wächter im sicheren Modus (`--watchdog <pid> --secure`): wartet 500 ms (Desktopwechsel
      beendet den ganzen Job, nur ein echter Absturz lässt den Wächter leben), startet mit
      `--secure-desktop` neu, max. 3× in 5 min; danach Merker `secure-gave-up.txt` → aus bis
      Windows-Neustart, keine Benachrichtigung. Rauchtest: Wächter startet mit
- [ ] Wächter: im echten Job (ATBroker) prüfen, dass er mitstartet und mit beendet wird
- [x] Overlay zeigt nichts, solange kein Bild erfasst wurde (`DesktopDuplicator.HasImage`);
      Hot-Path-Änderung ist eine Bool-Abfrage ohne Allokation, kein eigener Benchmark
- [x] Logs als SYSTEM automatisch in SYSTEMs Profil (`FileLog` nutzt LocalAppData); JSON-Fehler
      werden nicht mit Inhalt geloggt
- [x] Logs: UIA/Tracking loggen keinen Text (einzige Meldung: „UI Automation is unavailable“)
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
