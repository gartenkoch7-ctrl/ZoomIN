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
- [x] Testlauf als SYSTEM auf `Winlogon`: Log zeigt `SYSTEM: True` und die Startzeit
      (Anmeldebildschirm 287 ms, UAC 170–290 ms bis zur Engine)
- [x] Startmeldung an die Erleichterte Bedienung (`EaseOfAccessNotifier`): ohne sie startet
      Windows ZoomIN bei Sperre/Strg+Alt+Entf/UAC nie
- [x] SYSTEM ohne SeTcbPrivilege: Benutzer-Token über `explorer.exe` der Sitzung (`SessionUserToken`)
- [x] Win+L und Strg+Alt+Entf starten ZoomIN (Log dev.108, 12:53), UAC ebenso
- [x] Sicherer Modus startet mit dem gespeicherten `ZoomOn` (dev.109; vorher immer aus)
- [x] Sichtprüfung dev.109: Sperre und Strg+Alt+Entf vergrößert (Nutzer, Log 12:57)
- [ ] Startzeit: erstes Bild nach ~1,4 s (UAC, Sperre, Strg+Alt+Entf, dev.109) – Ziel ≤ 1 s
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
- [x] `/security-review` des Branches (2026-11-03): ein Fund – SYSTEM konnte über eine
      umgeleitete AppData einen Netzwerkpfad öffnen. Behoben: Lesen impersoniert als Benutzer,
      nur lokale Festplattenpfade (`SessionUser`, `LocalPathRule`)

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

## Abschluss (Grilling 2026-11-12)
Anlass: Bestandsaufnahme vom 2026-11-12.
- Der Autostart-Eintrag (Run) war auf dem Testrechner leer.
- Der Start bei Sperre und UAC hängt am Win+U-Trick.
- Die Startzeit liegt bei etwa 1,4 s.
- Die Spike-Punkte und die Checkliste in Abschnitt 6 sind offen.
- M17–M19 laufen ungefiltert im sicheren Modus.

Kommt **vor `v1.0.0`**. Autostart: [ADR-0030](../adr/0030-autostart-ueber-erleichterte-bedienung.md).

### Entscheidungen
| # | Thema | Entscheidung |
|---|---|---|
| Q1 | Autostart | Erleichterte Bedienung, HKCU-Liste `Configuration` (wie NVDA); der Win+U-Trick entfällt |
| Q9 | Ausweichweg | Scheitert der Spike (Tray-App kommt nicht sauber aus dem Job), gilt ohne Rückfrage: geplante Aufgabe bei der Anmeldung, Win+U-Trick bleibt |
| Q10 | ZIP/Dev-Build | „Mit Windows starten“ nutzt weiter den Run-Eintrag |
| Q5 | Schalter | Zwei Schalter, beide im Setup vorausgewählt: „Mit Windows starten“ (HKCU, ohne Admin) und „Auch auf Anmelde- und Sperrbildschirm“ (HKLM + `SecureDesktopAccommodation`, mit Admin-Abfrage) |
| Q6 | Migration | Einmal umstellen: Run-Eintrag weg, Eintrag in der Liste dazu; ohne Run-Eintrag bleibt der Autostart aus; nur Log, kein Hinweis |
| Q2 | Sicherer Modus | Nur der Kern läuft: Zoom, Modi, Filter, Mauszeiger, Markierungen, Fokus/Caret, Übersicht, Profile. Aus sind Inseln, Einfrieren, Monitorwechsel, Fenster-Ziehen (Maus-Puffer/Achsensperre starten schon heute nur in der Tray-App) |
| Q11 | Kürzel dieser Funktionen | Im sicheren Modus nicht registriert; die Taste geht an Windows, keine Ansage |
| Q3 | Startzeit | Ziel ≤ 1 s bis zum ersten vergrößerten Bild; Ursache der ~1 s Lücke suchen, bei hohem Aufwand neu entscheiden |
| Q7 | Diagnose | Das Diagnosepaket fragt per UAC, ob die Logs der sicheren Instanz dazukommen |
| Q8 | Prüfung | Die sichere Instanz loggt alles Prüfbare (Z-Reihenfolge, Hotkey-Registrierung, Wächter im Job, Zeiten); ein Kurzbefehl speichert dort einen Debug-Schnappschuss; danach Checkliste mit dem Nutzer |
| Q4 | Zeitpunkt | Jetzt, vor `v1.0.0` |

### Parallelisierung
Integrationsbranch **`feat/m10-abschluss`** (von `develop`). Regeln: [AGENTS.md §11](../../AGENTS.md).
Es gibt keinen Schritt 0: Die drei Spuren teilen keine Bausteine.

```
Spur A: Spike Job → Autostart, Schalter, Migration, Setup ──┐
Spur B: Kern-Modus für den sicheren Desktop ───────────────┼─► Integration ─► Review ─► Testbuild ─► Checkliste (Nutzer) ─► develop
Spur C: Startzeit, Prüf-Logs, Schnappschuss, Diagnose ─────┘
```

| Spur | Inhalt | Branch | Start | Wartet auf | EventIds |
|---|---|---|---|---|---|
| A | Spike Job-Ausstieg, `EaseOfAccessAutostart`, Schalter, Migration, Setup, Deinstallation | `feat/m10-a-autostart` | sofort | – | App 1120–1139, Platform 2320–2339, Setup 5040–5059 |
| B | `EngineScope` und Kern-Modus mit Tests | `feat/m10-b-kern-modus` | sofort | – | App 1140–1149 |
| C | Startzeit, Prüf-Logs der sicheren Instanz, Schnappschuss-Kürzel, SYSTEM-Logs im Diagnosepaket | `feat/m10-c-pruefung` | sofort | – | App 1150–1169, Platform 2340–2359 |

**Merge-Reihenfolge:** B → C → A.
- B ist klein und ändert nur die Zusammenstellung.
- C misst danach den Start des schlankeren Modus.
- A kommt zuletzt, weil der Ausgang des Spikes offen ist.

**Exklusiv:**
- **A:**
  - `Platform/Lifecycle/*`
  - `Platform/SecureDesktop/EaseOfAccessNotifier.cs`, `SecureDesktopRegistration.cs`, `SystemSecureDesktopInstall.cs`
  - `App/SecureDesktop/SecureDesktopSwitch.cs`, `SecureDesktopSetting.cs`
  - `App/ViewModels/GeneralViewModel.cs`, `WelcomeViewModel.cs`
  - `App/Updates/UninstallFlow.cs`, `App/App.xaml.cs`
  - `Setup/Install/*`
- **B:**
  - `App/Composition/EngineRegistration.cs`, `ServiceRegistration.cs`
  - `App/Zoom/HotkeyBindings.cs`, `ZoomController.cs`
  - neu `Core/SecureDesktop/EngineScope.cs`
- **C:**
  - `App/SecureDesktop/SecureDesktopHost.cs`, `SecureHostLog.cs`
  - `Platform/Diagnostics/DiagnosticsPackage.cs`
  - neue Dateien unter `App/SecureDesktop/Checks/`

**Hotspots** (nur ergänzend):
- `Texts{German,English}*.cs`
- Argumente des erhöhten Schritts (A: Autostart-Schalter, C: Logs kopieren)
- `docs/kb/telemetrie.md`, `docs/kb/sichere-desktops.md`

### Detailplan
Einstufung nach [AGENTS.md §13](../../AGENTS.md). Das Review jeder Spur läuft auf Opus.

**Paket A1 – Spike Job-Ausstieg (Stufe 3, Opus, `high`)**

Die Frage: Kann die Instanz, die die Erleichterte Bedienung bei der Anmeldung im Job startet, die
Tray-App außerhalb des Jobs weiterlaufen lassen?

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Platform/Interop/…` | ändern | `IsProcessInJob`, `QueryInformationJobObject` (Limit `JOB_OBJECT_LIMIT_BREAKAWAY_OK`), `CREATE_BREAKAWAY_FROM_JOB` per CsWin32 | – | 20 |
| `docs/kb/sichere-desktops.md` | ändern | Messung: Job-Limits beim Anmeldestart, Ausstieg gelingt ja/nein; Verhalten mit `TerminateOnDesktopSwitch=0` (bleibt die sichere Instanz stehen, wie lange dauert der zweite Start?) | – | 30 |

- **Ablauf:** Von Hand registrieren (HKCU `Configuration` = `ZoomIN`), abmelden und anmelden, dann
  das Log lesen. Danach Win+L: Läuft die Tray-App weiter? Startet die sichere Instanz?
- **Akzeptanz:** Das Ergebnis steht als Nachtrag in ADR-0030. Scheitert der Spike, gilt Q9, und
  A2 baut statt der Liste eine geplante Aufgabe (`TaskSchedulerAutostart`, Trigger „bei Anmeldung“,
  ohne Verzögerung, nur aktueller Benutzer).

**Paket A2 – Autostart über die Erleichterte Bedienung (Stufe 2, Sonnet, `medium`)**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Platform/Lifecycle/EaseOfAccessAutostart.cs` | neu | `IAutostart`. `IsEnabled` liest die HKCU-Liste `…\Accessibility\Configuration` (kommagetrennt, Groß/klein egal). `SetEnabled` fügt `ZoomIN` hinzu oder entfernt es und lässt andere Namen stehen. Konstruktor mit `RegistryKey` für Tests, wie `RegistryAutostart` | `EaseOfAccessAutostartTests`: leer, mit `nvda_nvda_v1`, doppelt, Leerzeichen, Entfernen lässt andere stehen | 80 |
| `Platform/Lifecycle/AutostartSelector.cs` | neu | Installiert (`InstallLayout.TryFromExecutable`) → `EaseOfAccessAutostart`, sonst `RegistryAutostart` | `AutostartSelectorTests` | 40 |
| `Platform/Lifecycle/AutostartMigration.cs` | neu | `Migrate()`: Nur wenn installiert und ein Run-Wert `ZoomIN` existiert → Run-Wert löschen und Liste setzen; Log 2320 | `AutostartMigrationTests`: mit/ohne Run-Wert, ZIP-Layout ändert nichts | 60 |
| `Platform/Lifecycle/AutostartLog.cs` | neu | `[LoggerMessage]` 2320–2324 | – | 30 |
| `App/Composition/ServiceRegistration.cs` | ändern | `IAutostart` über `AutostartSelector` (Zeilen 57 und 108); gehört B, darum **nach dem Merge von B** in der Integration | – | 4 |
| `App/App.xaml.cs` | ändern | `AutostartMigration.Migrate()` beim Start; `EaseOfAccessNotifier` nur noch im Ausweichweg aus Q9 | – | 10 |
| `Platform/SecureDesktop/SecureDesktopRegistration.cs` | ändern | Schalter „Sperrbildschirm“ aus → `SecureDesktopAccommodation=none` setzen und HKLM `Configuration` ohne ZoomIN; an → Wert löschen. `ATs\ZoomIN` bleibt registriert, solange installiert | bestehende Tests erweitern | 30 |
| `Setup/Install/SetupInstaller.cs` | ändern | Zeile 91: `AutostartSelector` statt fest `RegistryAutostart` | Setup-Test | 5 |
| `App/Updates/UninstallFlow.cs` | ändern | Entfernt den Listeneintrag und einen alten Run-Wert | bestehender Test | 5 |
| `App/ViewModels/GeneralViewModel.cs` | ändern | Der Schalter liest `IsEnabled` beim Öffnen der Seite (Windows-Einstellungen können ihn ändern) | ViewModel-Test | 10 |
| `Texts*` | ändern | Beschriftung „Mit Windows starten (sofort nach der Anmeldung)“, DE/EN | – | 4 |

- **Akzeptanz:**
  - `dotnet test` grün.
  - Nach dem Neustart des Testbuilds steht ZoomIN in der Liste, und der Run-Wert ist weg.
  - Win+L startet die sichere Instanz ohne Win+U (Log).
- **Telemetrie:** Ablauf `zoomin.autostart.migrate` (Tag `zoomin.autostart.from`: `run`, `none`).

**Paket B1 – Kern-Modus (Stufe 2, Sonnet, `medium`)**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `Core/SecureDesktop/EngineScope.cs` | neu | `enum EngineScope { Full, SecureDesktop }` | – | 10 |
| `App/Composition/EngineRegistration.cs` | ändern | `AddEngine(this IServiceCollection, EngineScope scope)` registriert `scope` als Singleton. `IslandMapping` liefert bei `SecureDesktop` ein leeres `IslandModel` | – | 10 |
| `App/Composition/ServiceRegistration.cs` | ändern | `SecureDesktop(…)` ruft `AddEngine(EngineScope.SecureDesktop)` auf, die Tray-App `Full` | – | 4 |
| `App/Zoom/HotkeyBindings.cs` | ändern | Bei `SecureDesktop` werden `IslandsHotkey`, `IslandEditHotkey`, `FreezeMonitorHotkey` und `NextMonitorHotkey` nicht registriert und nicht als „nicht verfügbar“ gemeldet (Zeilen 68–73) | `HotkeyBindingsScopeTests` | 15 |
| `App/Zoom/ZoomController.cs` | ändern | `windowDrag.Start()` nur bei `Full` | Test | 5 |
| `tests/…/SecureEngineScopeTests.cs` | neu | Baut `ServiceRegistration.SecureDesktop(…)` mit Fakes. Prüft: keine Insel-, Einfrier- oder Monitorwechsel-Kürzel registriert, Fenster-Ziehen nicht gestartet, `IPointerFence` ohne Besitzer. Prüft auch: Die Haltetaste der Markierungen (M18) liefert über ihre Schnittstelle nur Haltetaste an/aus (Datenschutz wie `InputActivityPrivacyTests`) | ja | 120 |

- **Akzeptanz:** neue Tests grün, Architekturtests grün.
- **Telemetrie:** Log 1140 „Secure desktop runs the core engine (scope {Scope})“ beim Start.

**Paket C1 – Startzeit (Stufe 3, Opus, `high`)**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `App/SecureDesktop/SecureDesktopHost.cs` | ändern | Zwischenzeiten als Kinder von `zoomin.secure.start`: erstes Duplication-Bild, erste Präsentation, Ende der Einblend-Animation | – | 20 |
| Fundstelle der Lücke | ändern | Ursache beheben. Vermutung: Die Zoom-Animation beginnt bei 1× und gleitet etwa 1 s; im sicheren Modus sollte die Stufe sofort gelten. Sonst wartet `AcquireNextFrame` auf eine Bildänderung | je nach Ursache | 30 |

- **Akzeptanz:** Log „first magnified image after … ms“ ≤ 1000 ms bei UAC und bei Win+L
  (Testbuild). Hot Path geändert → Benchmark `0 B`.

**Paket C2 – Prüf-Logs und Schnappschuss (Stufe 3, Opus, `high`)**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `App/SecureDesktop/Checks/SecureDesktopChecks.cs` | neu | Einmal nach dem ersten Bild loggen: Desktopname (`GetUserObjectInformation`), ob das Overlay das oberste Fenster an der Monitormitte ist (`WindowFromPoint` = eigenes HWND), Ergebnis von `GetWindowDisplayAffinity`, Prozess im Job (`IsProcessInJob`), Wächter läuft (PID), Ergebnis jeder Hotkey-Registrierung | Fakes für die Win32-Abfragen | 120 |
| `App/SecureDesktop/SecureHostLog.cs` | ändern | 1150–1159 | – | 30 |
| `App/SecureDesktop/SecureDesktopHost.cs` | ändern | Kurzbefehl `Strg+Alt+Umschalt+D` (nur im sicheren Modus, wie das Beenden-Kürzel) → `DebugSnapshots` in SYSTEMs Ordner `…\ZoomIN\debug` | Test gegen die AltGr-Regel | 20 |

- **Akzeptanz:** Ein Rauchtest als normaler Benutzer (`--secure-desktop`) schreibt alle Prüfzeilen.

**Paket C3 – SYSTEM-Logs im Diagnosepaket (Stufe 3, Opus, `high`, Sicherheitsprüfung)**

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| Argumente des erhöhten Schritts | ändern | Neues Argument `--copy-secure-logs <Zielordner>`. Prüft, dass das Ziel unter dem Diagnoseordner des aufrufenden Benutzers liegt (`LocalPathRule`, kein Link). Kopiert nur `*.log`/`*.jsonl` aus SYSTEMs `ZoomIN\logs` und `debug`, höchstens 20 MB | Pfadprüfung mit Junction, UNC, `..` | 80 |
| `Platform/Diagnostics/DiagnosticsPackage.cs` | ändern | Mit der Option „Logs der sicheren Instanz“ wird erst der erhöhte Schritt ausgeführt, dann der Ordner ins ZIP aufgenommen. Lehnt der Nutzer die UAC-Abfrage ab → ZIP ohne diese Logs, Vermerk in `ZoomIN-Info.txt` | Test mit Fake-Starter | 40 |
| Diagnose-Seite, `Texts*` | ändern | Kontrollkästchen „Logs von Anmelde- und Sperrbildschirm (Adminrechte)“ | – | 20 |

- **Akzeptanz:** `/security-review` ohne Befund.

**Paket D – KB, Telemetrie-Katalog, Häkchen (Stufe 1, Haiku, `low`)**
- `telemetrie.md`: Bereiche und Einträge eintragen.
- `sichere-desktops.md`: Stand nach dem Spike.
- Hier im Plan die Häkchen setzen.

### Aufgaben (Abschluss)
- [ ] Integrationsbranch `feat/m10-abschluss`, Spuren A, B, C gleichzeitig (Worktree je Spur)
- [x] Spur A: Paket A2 umgesetzt (`feat/m10-a-autostart`); Job-Prüfung und Ausstieg für A1 gebaut,
  Win+U-Trick nur noch ohne Start durch die Erleichterte Bedienung
- [ ] Integration nach dem Merge von B: `ServiceRegistration.cs` Zeilen 57 und 108
  `IAutostart` → `AutostartSelector` (sonst nutzen Schalter, Willkommen und Deinstallation weiter
  nur den Run-Eintrag, während die Migration schon in die Liste umzieht); vor dem Testbuild für A1
- [ ] Spike A1, Ergebnis als Nachtrag in ADR-0030 – Messung mit dem Nutzer offen, Anleitung in
  [sichere-desktops.md](../kb/sichere-desktops.md#spike-a1-durchführen-nutzer-offen)
- [ ] Ausweichweg Q9 (`TaskSchedulerAutostart`, geplante Aufgabe bei der Anmeldung): ausstehend,
  nur falls der Spike scheitert – noch nicht gebaut
- [ ] Mergen B → C → A, nach jedem Merge Build und Tests
- [ ] Gesamt-Review (§9) bis ohne Befunde; `/security-review`
- [ ] Testbuild installieren (Nutzer), dann die Checkliste in Abschnitt 6 und zusätzlich:
  - [ ] Nach der Anmeldung läuft ZoomIN sofort (kein Warten auf den Run-Eintrag)
  - [ ] Win+L, Strg+Alt+Entf und UAC vergrößert ohne Win+U-Trick; erstes Bild ≤ 1 s
  - [ ] Schalter „Sperrbildschirm“ aus: Autostart läuft, auf der Sperre startet nichts
  - [ ] Prüf-Logs: Overlay oben, Ausschluss aus Aufnahmen gesetzt, Wächter im Job, Kürzel registriert
  - [ ] Inseln, Einfrieren und Monitorwechsel fehlen auf der Sperre, die Kürzel gehen an Windows
  - [ ] Diagnosepaket mit Logs der sicheren Instanz
- [ ] Roadmap-Status, Merge nach `develop` nur mit Freigabe
