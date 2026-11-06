# Telemetrie – Logs, Abläufe, Messwerte

Grundlage: [ADR-0019](../adr/0019-telemetrie-lokal-mit-opentelemetry.md), Plan:
[M11](../plan/m11-diagnose-und-telemetrie.md). Regeln für neuen Code: [AGENTS.md 2.5](../../AGENTS.md).

## Wie es zusammenhängt
- **Core** kennt nur `Telemetry.Source` (`ActivitySource` „ZoomIN“), `Telemetry.Meter` und
  `ILogger<T>` aus `Microsoft.Extensions.Logging.Abstractions`. Ohne Zuhörer kosten sie nichts.
- **Platform/Diagnostics/TelemetryHost** startet in jedem Prozess dasselbe: OpenTelemetry-SDK,
  Datei-Exporter, optional OTLP. Jede Rolle setzt `zoomin.role`
  (`app`, `watchdog`, `update`, `elevated`, `uninstall`, `secure`, `setup`).
- Die Logger werden einmal beim Start verteilt (`SwitchableLoggerProvider`); dahinter wird die
  Pipeline neu gebaut, wenn Stufe oder Export-Adresse wechseln – **ohne Neustart**.
- Logs entstehen nur über `[LoggerMessage]`-Methoden in einer `*Log.cs` je Feature-Ordner
  (Analyzer CA1848 erzwingt das). Argumente, die erst berechnet werden müssten, vorher in eine
  lokale Variable legen (CA1873).

## Ablage auf dem PC
- Ordner `%LocalAppData%\ZoomIN\logs` (sichere Bildschirme: im Profil von SYSTEM).
- **Eine Datei pro Stunde und Art:** `zoomin-JJJJMMTT-HH.log` (Textzeilen) und
  `zoomin-JJJJMMTT-HH.trace.jsonl` (ein abgeschlossener Ablauf je Zeile). Stündlich statt
  täglich, damit „1 Tag aufbewahren“ wirklich höchstens 24 Stunden bedeutet.
- Logzeile: `2026-11-05 21:03:51.123 [WARN ] app#1234 Win32HotkeyService[2201]: … trace=… span=…`.
  `trace`/`span` verknüpfen die Zeile mit dem Ablauf in der `.trace.jsonl`.
- **Aufbewahrung:** Standard 1 Tag, bis 7 Tage. Aufgeräumt beim Start und stündlich; alte
  Tagesdateien von vor M11 (`zoomin-JJJJMMTT.log`) werden mit entfernt.
- **Grenze:** 20 MB je Kalendertag (alle Dateien des Tages). Darüber nur noch Fehler und
  fehlgeschlagene Abläufe, mit einer Hinweiszeile.
- Geschrieben wird gebündelt im Hintergrund (Batch, 1 s). Vor einem harten Ende
  `Telemetry.FlushBeforeExit()`; der Absturz-Handler in `Program` schreibt den Puffer selbst.
- Mehrere ZoomIN-Prozesse schreiben gleichzeitig in dieselben Dateien (Append, geteilter Zugriff).

## Stufen (Einstellungen `Telemetry*`, `TelemetryOptions`)
| Stufe | Logs ab | Abläufe | Messwerte |
|---|---|---|---|
| Nur Fehler | Fehler | – | – |
| **Normal** (Standard) | Warnung | ✓ | – |
| Ausführlich | Debug | ✓ | ✓ |
| Benutzerdefiniert | Schalter + Stufe | Schalter | Schalter |

- Fehler und Abstürze werden **immer** geschrieben.
- „Ausführlich“, „Benutzerdefiniert“ und mehr als 1 Tag gelten 7 Tage (`TelemetryRaisedUntil`),
  dann zurück auf Normal / 1 Tag. Ein Wert ohne Ende (von Hand eingetragen) fällt sofort zurück.
- Kurzlebige Rollen (Update, Hilfsprozesse, Deinstallation, Setup) lesen keine Einstellungen und
  laufen mit „Normal“. Die App und der sichere Modus wenden die Einstellung nach dem Laden an.
- Messwerte gibt es nur mit Export-Adresse (sie werden nicht in Dateien geschrieben).

## Export (OTLP, opt-in)
- Adresse aus `TelemetryOtlpEndpoint`, sonst `OTEL_EXPORTER_OTLP_ENDPOINT`. Leer = nichts verlässt
  den PC. Port 4318 → HTTP/Protobuf (Signalpfad wird ergänzt), sonst gRPC.
- Der OTLP-Exporter steckt nur in `OtlpExport`; ohne Adresse wird seine Assembly nicht geladen.
- **Rolle `secure` exportiert nie** – der Sanitizer leert die Adresse zusätzlich.
- Zum Ansehen: Aspire Dashboard oder Jaeger lokal starten, Adresse `http://localhost:4317`.

## Ein Trace über Prozesse
- Startet ZoomIN sich selbst (Watchdog, Neustart nach Update, Hilfsprozess mit `runas`, Setup ↔
  App), hängt es `--traceparent <W3C-Id>` an (`TraceParentArgument.Append`). Jeder Einstiegspunkt
  nimmt das Argument zuerst heraus (`Extract`), danach parsen die Rollen wie bisher.
- Argument statt Umgebungsvariable, weil „runas“ die Umgebung nicht verlässlich weitergibt;
  `TRACEPARENT` wird als Rückfall gelesen.

## Datenschutz
`TelemetryPrivacy.IsAllowedAttribute` – verboten: Schlüssel mit `title`, `text`, `content`,
`document`, `clipboard`; letztes Segment `path`, `url`, `uri`, `fullpath`; `element.name`,
`window.name`. Der Datei-Exporter verwirft solche Attribute, `TelemetryConventionTests` prüft alle
`SetTag`-Schlüssel und Log-Platzhalter im Code. Freitext in Lognachrichten braucht Review.
Bei M11 entfernt: volle Pfade in Settings-, Profil-, Backup- und Update-Meldungen.

## EventId-Bereiche
| Bereich | Projekt |
|---|---|
| 1000–1999 | App (1000 Lebenszyklus, 1100 sicherer Bildschirm, 1200 Updates, 1300 Zoom) |
| 2000–2999 | Platform (2000 Automation, 2100 Zeiger, 2200 Eingabe, 2300 sicherer Bildschirm, 2400 Einstellungen, 2500 Shell, 2600 Updates, 2700 Filter pro Fenster) |
| 3000–3999 | Rendering |
| 4000–4999 | Watchdog |
| 5000–5999 | Setup |
| 6000–6999 | Core (noch keine) |
| 9000–9099 | Tests |

## Katalog: Abläufe
| Name | Rolle | Dauer von … bis … |
|---|---|---|
| `zoomin.app.start` | app | Prozessstart bis Tray-App bereit |
| `zoomin.secure.start` | secure | Prozessstart bis Engine läuft bzw. erstes vergrößertes Bild (Tag `zoomin.secure.zoom_on`) |
| `zoomin.watchdog.run` | watchdog | Überwachung, bis die App endet |
| `zoomin.update.run`, `zoomin.elevated.run`, `zoomin.uninstall.run` | jeweilige Rolle | ganzer Lauf; Exit-Code ≠ 0 → Status Error |
| `zoomin.setup.install` | setup | Installation im Assistenten (Tag `zoomin.version`) |
| `zoomin.setup.apply` | setup | Installationsschritt mit Adminrechten |
| `zoomin.render.pipeline_start` | app, secure | Aufbau von Gerät, Duplication und Overlay je Monitor (Tag `zoomin.monitor`; Fehler: Ausnahmetyp) |
| `zoomin.render.rebuild` | app, secure | Neuaufbau aller Renderer, z. B. nach Anzeigeänderung (Tag `zoomin.monitors`) |
| `zoomin.update.check` | app | Release-Liste holen (Tags `zoomin.update.channel`, `zoomin.update.releases`) |
| `zoomin.update.install` | app | Update per Klick: Download, ggf. Sicherung, Installationsschritt (Tags `zoomin.version`, `zoomin.update.result`) |
| `zoomin.update.download` | app, setup | Prüfsummenliste + Paket bzw. Setup (Tag `zoomin.package`, Ergebnis) |
| `zoomin.update.backup` | app | Einstellungen sichern vor einem Downgrade |
| `zoomin.update.elevate` | app, setup | Startet den Schritt mit Adminrechten; dessen `zoomin.update.run`/`zoomin.setup.apply` hängt darunter |
| `zoomin.update.apply` | update, setup | Paket prüfen, entpacken, `current` umschalten (Ergebnis) |
| `zoomin.update.verify` | update, setup | Signatur der neuen `ZoomIN.exe` (Ergebnis z. B. `UnexpectedPublisher`) |
| `zoomin.update.restart` | app | Neustart nach dem Update; Kind von `zoomin.update.install`, darunter `zoomin.app.start` der neuen Version |
| `zoomin.setup.launch` | app | Setup herunterladen, prüfen, starten (Ergebnis); das Setup setzt den Trace fort |
| `zoomin.secure.settings` | secure | Woher die Einstellungen kommen (Tag `zoomin.secure.settings_source`: `user`, `copy`, `default`) |
| `zoomin.settings.load` | app, setup | `settings.json` lesen (Tag `zoomin.settings.result`: `ok`, `missing`, `unreadable` → Error) |
| `zoomin.settings.save` | app, setup | `settings.json` schreiben (Fehler: Ausnahmetyp) |
| `zoomin.profile.switch` | app | Profilwechsel (ohne Profilnamen – die vergibt der Nutzer frei) |
| `zoomin.diagnostics.package` | app | Diagnosepaket erstellen (Tag `zoomin.diagnostics.files`) |
| `zoomin.windowfilter.toggle` | app | Kürzel „Filter für aktives Fenster“ (Tags `process.name`, `zoomin.choice`: `None`, `Filter`) |

## Katalog: Messwerte
Nur mit Export-Adresse und Stufe „Ausführlich“/„Benutzerdefiniert + Messwerte“. Im Render-Loop
ohne Allokation, auch mit dem SDK als Zuhörer (`RenderMetricsTests`).

| Name | Einheit | Tags | Bedeutung |
|---|---|---|---|
| `zoomin.render.frame_interval` | ms | `zoomin.monitor` | Abstand zwischen zwei gezeigten Frames; Ausreißer = Ruckler |
| `zoomin.render.frames_missed` | Frames | `zoomin.monitor` | Compositor hat keinen Frame rechtzeitig (100 ms) angefordert |
| `zoomin.render.show_latency` | ms | `zoomin.monitor` | Aufwachen (Zoom/Filter an) bis erster sichtbarer Frame |
| `zoomin.render.pipeline_restarts` | Neustarts | `zoomin.monitor`, `zoomin.reason` (`failure`, `display_changed`) | Pipeline neu aufgebaut |
| `zoomin.tracking.read_duration` | ms | – | Eine Abfrage von Fokus und Caret; lange Werte = App antwortet langsam |
| `zoomin.tracking.caret_sources` | Quellen | `process.name`, `zoomin.caret.source` (`system`, `msaa`, `uia`, `estimated`, `cell`) | Caret-Quelle zum ersten Mal in einem Prozess gesehen (dazu Log 2002) |
| `zoomin.tracking.chromium_nudges` | Fenster | – | Chromium-Inhaltsfenster nach IAccessible2 gefragt |
| `zoomin.tracking.anchor_switches` | Wechsel | `zoomin.anchor.source` (`mouse`, `focus`, `caret`) | Ansicht folgt jetzt Maus, Fokus oder Caret (Render-Thread, ohne Allokation) |
| `zoomin.render.overlay_time` | s | `zoomin.monitor`, `zoomin.state` (`active`, `paused`) | Zeit, in der der Render-Thread arbeitet bzw. schläft – ergibt den Pausenanteil des Filters pro Fenster |
| `zoomin.windowfilter.classifications` | Fenster | `process.name`, `zoomin.result` (`light`, `dark`, `unknown`) | Fenster beim Öffnen eingestuft (dazu Log 2701) |
| `zoomin.windowfilter.look_duration` | ms | – | Ein Blick auf ein Fenster (GDI-Kopie 64×64 und Zählen) |
| `zoomin.windowfilter.update_duration` | ms | – | Fenster auflisten und Bereiche bauen nach einer Fensteränderung |
| `zoomin.windowfilter.regions` | Bereiche | – | Bereiche, die der Shader nach einer Änderung bekommt (höchstens 64) |
| `zoomin.windowfilter.regions_dropped` | Fenster | – | Fenster über dem Limit von 64 Bereichen, die den Monitorfilter zeigen (je Aktualisierung; dazu Log 2705) |

## Katalog: Logmeldungen
| Id | Stufe | Meldung |
|---|---|---|
| 1001 | Info | ZoomIN started after {Milliseconds} ms |
| 1002 | Info | ZoomIN exits |
| 1003 | Fehler | Unhandled UI exception |
| 1004 | Kritisch | Unhandled exception, ZoomIN ends |
| 1005 | Fehler | Unobserved task exception |
| 1006 | Warnung | UIAccess is not active: quick settings and Start stay above the zoom |
| 1007 | Info | Update checks stopped: {Reason} |
| 1008 | Info | Debugger attached: watchdog not started |
| 1009 | Info | Watchdog started |
| 1010 | Fehler | Starting the watchdog failed |
| 1101 | Warnung | Secure desktop: off until Windows restarts (watchdog gave up after repeated crashes) |
| 1102 | Info | ZoomIN secure desktop started (SYSTEM: {IsSystem}) |
| 1103 | Info | ZoomIN secure desktop exits |
| 1104 | Warnung | Exit shortcut for the secure desktop is unavailable |
| 1105 | Info | Secure desktop engine running after {Milliseconds} ms (zoom on: {ZoomOn}) |
| 1106 | Warnung | Secure desktop: no magnified image after {Seconds} s |
| 1107 | Info | Secure desktop: first magnified image after {Milliseconds} ms |
| 1108 | Info | Secure desktop support switched {State} |
| 1109 | Fehler | Changing the secure desktop registration failed |
| 1110 | Fehler | Starting the secure desktop switch failed |
| 1201 | Info | Setup: release list unavailable: {Reason} |
| 1202 | Warnung | Setup {Version} not started: {Reason} |
| 1203 | Info | Setup {Version} started |
| 1204 | Info | ZoomIN uninstalled |
| 1205 | Info | Uninstall: removal scheduled |
| 1206 | Fehler | Uninstall: starting the removal failed |
| 1207 | Fehler | Update: this ZoomIN.exe is not a versioned installation |
| 1208 | Fehler | Update: refreshing the Apps entry failed |
| 1209 | Info | Update available: {Version} |
| 1301 | Debug | Hotkey {Gesture} registered |
| 2001 | Fehler | UI Automation is unavailable; focus tracking is off |
| 2002 | Info | Caret source in {Process}: {Source} |
| 2101 | Warnung | MagInitialize failed: the system pointer stays visible while zoomed |
| 2201 | Warnung | Hotkey {Gesture} is already in use by another application |
| 2202 | Fehler | Hotkey handler failed |
| 2203 | Fehler | Installing the activity hooks failed; shake/Ctrl locator and hiding while typing are unavailable |
| 2204 | Fehler | Input activity handler failed |
| 2205 | Fehler | Installing the mouse wheel hook failed; Ctrl+wheel zoom is unavailable |
| 2206 | Fehler | Wheel zoom handler failed |
| 2301 | Info | Ease of Access notified ({Signal}) |
| 2302 | Warnung | Ease of Access not notified ({Error}) |
| 2303 | Info | Secure desktop support {State} |
| 2304 | Fehler | Registering for the secure desktop failed |
| 2305 | Fehler | Removing the secure desktop support failed |
| 2306 | Info | Secure desktop: using {Source} settings |
| 2307 | Warnung | Secure desktop: the session user's settings are missing, refused or invalid |
| 2308 | Warnung | Secure desktop: settings copy not written ({Error}) |
| 2309 | Warnung | Secure desktop: session id unknown (error {Error}) |
| 2310 | Info | Secure desktop: the user's AppData is not on a local fixed disk |
| 2311 | Warnung | Secure desktop: the user's AppData is unknown (0x{Result:X8}) |
| 2312 | Info | Secure desktop: no signed-in user in session {Session} (error {Error}) |
| 2313 | Info | Secure desktop: no user shell in session {Session} |
| 2314 | Info | Secure desktop: using the token of the user's shell |
| 2401 | Info | Settings backed up to {Folder} |
| 2402 | Fehler | Settings backup failed |
| 2403 | Warnung | Profile {File} is unreadable: {Reason} |
| 2404 | Fehler | Settings file {File} is unreadable; using defaults |
| 2405 | Fehler | Saving settings to {File} failed |
| 2406 | Warnung | Could not back up broken settings file: {Reason} |
| 2501 | Info | Docked strip reserved: {Edge} {Left},{Top}–{Right},{Bottom} |
| 2502 | Info | Docked strip released |
| 2503 | Warnung | Adding the tray icon failed |
| 2504 | Fehler | Tray command {Command} failed |
| 2601 | Info | Install step for {Version}: {Result} |
| 2602 | Fehler | Starting the install step failed |
| 2603 | Info | Release list skipped: {Reason} |
| 2604 | Warnung | Package {Package} rejected: checksum mismatch |
| 2605 | Info | Package {Package} not downloaded: {Reason} |
| 2606 | Info | Update {Previous} -> {Version} installed |
| 2607 | Fehler | Installing {Version} failed |
| 2608 | Info | Update: {Item} kept for now, still in use |
| 2700 | Info | Filter per window watches windows ({Hooks} event hooks) |
| 2701 | Debug | Window of {Process} measured as {Result} in {DurationMs} ms after {Looks} looks |
| 2702 | Warnung | Window events unavailable; the filter per window only follows shortcuts and settings |
| 2703 | Fehler | Updating the window regions failed |
| 2704 | Warnung | No bitmap for brightness looks; windows stay unmeasured |
| 2705 | Info | {Dropped} windows over the limit of {Max} regions show the monitor filter |
| 3001 | Info | Zoom pipeline ready for {Monitor} |
| 3002 | Info | Display changed on {Monitor}: {Reason} |
| 3003 | Fehler | Zoom pipeline for {Monitor} failed |
| 3004 | Info | Monitors: {Monitors} |
| 3005 | Kritisch | A render thread does not stop; restarting ZoomIN via the watchdog |
| 3006 | Fehler | Render thread without progress for over {Seconds} s; rebuilding the zoom pipeline |
| 4001 | Warnung | Cannot watch process {ProcessId}: {Reason} |
| 4002 | Fehler | ZoomIN ended unexpectedly (exit code 0x{ExitCode:X8}) |
| 4003 | Warnung | Restarting ZoomIN |
| 4004 | Fehler | Giving up after {Restarts} restarts within {Minutes} minutes |
| 4005 | Info | Cleanup {Step} done |
| 4006 | Fehler | Cleanup {Step} failed |
| 5001 | Info | Setup: ZoomIN {ProcessId} not ended: {Reason} |
| 5002 | Fehler | Setup: registering the installation failed |
| 5003 | Info | Setup: old Inno installation removed |
