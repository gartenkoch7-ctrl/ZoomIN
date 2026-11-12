# Telemetrie – Logs, Abläufe, Messwerte

Grundlage: [ADR-0019](../adr/0019-telemetrie-lokal-mit-opentelemetry.md), Plan:
[M11](../plan/m11-diagnose-und-telemetrie.md). Regeln für neuen Code: [AGENTS.md 2.5](../../AGENTS.md).

## Wie es zusammenhängt
- **Core** kennt nur `Telemetry.Source` (`ActivitySource` „ZoomIN“), `Telemetry.Meter` und
  `ILogger<T>` aus `Microsoft.Extensions.Logging.Abstractions`. Ohne Zuhörer kosten sie nichts.
- **Platform/Diagnostics/TelemetryHost** startet in jedem Prozess dasselbe: OpenTelemetry-SDK,
  Datei-Exporter, optional OTLP. Jede Rolle setzt `zoomin.role`
  (`app`, `watchdog`, `update`, `elevated`, `uninstall`, `secure`, `setup`, `mcp`).
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
- Kurzlebige Rollen (Update, Hilfsprozesse, Deinstallation, Setup, MCP-Server) lesen keine Einstellungen und
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
| 1000–1999 | App (1000 Lebenszyklus, 1100 sicherer Bildschirm, 1200 Updates, 1300 Zoom, 1400 KI-Debug, 1500 Maus folgen, 1520 Übersicht, 1540 Fenster-Verschieben, 1720 eingefrorene Monitore und Monitorwechsel) |
| 2000–2999 | Platform (2000 Automation, 2100 Zeiger, 2200 Eingabe, 2300 sicherer Bildschirm, 2400 Einstellungen, 2500 Shell, 2600 Updates, 2700 Filter pro Fenster, 2800 KI-Debug-Pipe, 2900 Fenster-Verschieben, 2920 Übersicht, 2940 Zeigerzaun und Zeiger versetzen) |
| 3000–3999 | Rendering (3100 Übersicht, 3300 Standbild, 3310 eingefrorene Monitore) |
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
| `zoomin.app.start.services`, `zoomin.app.start.shell` | app | Kinder von `zoomin.app.start` (M14): DI-Container, Einstellungen laden, Watchdog starten bzw. Engine, Hooks, Tray und Erststart |
| `zoomin.app.start.shell.resolve`, `.engine`, `.tray`, `.notify` | app | Kinder von `zoomin.app.start.shell` (M14): Objektgraph von `AppShell` bauen, Engine starten (Kürzel, Hooks, Ausgabe, Tracking, Fensterfilter), Tray-Menü/Erststart/Hinweise, zweiter Start und Erleichterte Bedienung |
| `zoomin.render.pipeline_start` | app, secure | Aufbau von Gerät, Duplication und Overlay je Monitor (Tag `zoomin.monitor`; Fehler: Ausnahmetyp) |
| `zoomin.render.pipeline_start.device`, `.capture`, `.overlay`, `.shaders` | app, secure | Kinder von `zoomin.render.pipeline_start` (M14): D3D-Gerät, Desktop Duplication, Overlay-Fenster und Swapchain, Shader kompilieren und Durchgänge anlegen |
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
| `zoomin.debug.snapshot` | app | Debug-Schnappschuss eines Monitors schreiben (Tag `zoomin.monitor`; Fehler: Ausnahmetyp) |
| `zoomin.windowfilter.toggle` | app | Kürzel „Filter für aktives Fenster“ (Tags `process.name`, `zoomin.choice`: `None`, `Filter`) |
| `zoomin.view.overview` | app | Übersicht (M17 B2) vom Kürzel bis zum Hineinzoomen, Esc oder Abbruch; eigener Wurzel-Ablauf über Threads hinweg. Tags `zoomin.monitor`, `zoomin.mode`, `zoomin.overview.level_before` (Stufe vorher), am Ende `zoomin.overview.kind` (`tap`, `hold`) und `zoomin.overview.end` (`zoom_in`, `escape`, `cancelled`) |
| `zoomin.agent.command` | app | Ein KI-Debug-Befehl über die Pipe (ADR-0027; Tag `zoomin.agent.tool`: Werkzeugname oder `unknown`; Fehler: Ausnahmetyp bzw. `failed`) |
| `zoomin.pointer.fence` | app, secure | Zeigerzaun neu berechnet und durchgesetzt (ADR-0028; Tags `zoomin.fence.mode`: `off`, `clip`, `hook`; `zoomin.fence.trigger`: `request`, `desktop_switch`, `session_unlock`, `display_change`; `zoomin.fence.forbidden`: Zahl der gesperrten Rechtecke) |
| `zoomin.pointer.move` | app | Zeiger per Kürzel versetzt (ADR-0028; Tags `zoomin.reason`: `FreezeMonitor`, `NextMonitor`; `zoomin.monitor`; abgelehnt → Status Error) |
| `zoomin.view.freeze` | app | Ein Monitor eingefroren (G2), vom Kürzel bis zum Aufheben; eigener Wurzel-Ablauf über Threads hinweg. Tags `zoomin.monitor`, `zoomin.monitor.number` (Windows-Nummer), `zoomin.zoom.level`, `zoomin.mode`, `zoomin.freeze.image` (`live`, `still`), am Ende `zoomin.freeze.end` (`command`, `display_change`, `desktop_switch`, `session_unlock`, `move_refused`, `shortcut_gone`, `failure`, `exit`) |
| `zoomin.view.monitor_switch` | app | `Strg+Alt+N` (G2): Tags `zoomin.monitor.from`, `zoomin.monitor.to`, `zoomin.monitor.skipped` (übersprungene eingefrorene Monitore); Versetzen abgelehnt → Status Error. Darunter `zoomin.pointer.move` |
| `zoomin.view.window_drag` | app, secure | Fenster verschieben oder Größe ändern (G1), von der ersten Fensteränderung bis zum Loslassen; nur wenn ein Ziehen etwas bewirken kann (Zoom an, nicht Angedockt, Aktion nicht „unverändert“, ein Schalter an). Tags `zoomin.drag.kind` (`move`, `resize`; `unknown` nur bei Umriss-Ziehen mit unklarer Trefferzone), `zoomin.drag.action` (`lift`, `lower`; `unchanged` nur, wenn die Einstellung zwischen Drücken und Ziehen wechselte), `zoomin.zoom.before` (Stufe des Nutzers), `zoomin.zoom.during` (vorübergehende Stufe; fehlt, wenn sie nichts absenkte), `zoomin.drag.crossed_monitor`, `zoomin.drag.cleared` (Befehl des Nutzers oder neue Monitoranordnung), `zoomin.monitor` (Monitor am Ende). Keine Fenstertitel |

## Katalog: Messwerte
Nur mit Export-Adresse und Stufe „Ausführlich“/„Benutzerdefiniert + Messwerte“. Im Render-Loop
ohne Allokation, auch mit dem SDK als Zuhörer (`RenderMetricsTests`).

| Name | Einheit | Tags | Bedeutung |
|---|---|---|---|
| `zoomin.render.frame_interval` | ms | `zoomin.monitor` | Abstand zwischen zwei gezeigten Frames; Ausreißer = Ruckler. Über eine Leerlauf-Lücke (M14) hinweg nicht erfasst |
| `zoomin.render.frames_missed` | Frames | `zoomin.monitor` | Compositor hat keinen Frame rechtzeitig (100 ms) angefordert; Leerlauf-Durchgänge zählen nicht |
| `zoomin.render.frames_skipped` | Frames | `zoomin.monitor`, `zoomin.reason` (`idle`) | Durchgänge ohne Änderung (M14): nichts gezeichnet, kein Present, der letzte Frame bleibt stehen |
| `zoomin.render.show_latency` | ms | `zoomin.monitor` | Aufwachen (Zoom/Filter an) bis erster sichtbarer Frame |
| `zoomin.render.pipeline_restarts` | Neustarts | `zoomin.monitor`, `zoomin.reason` (`failure`, `display_changed`) | Pipeline neu aufgebaut |
| `zoomin.tracking.read_duration` | ms | – | Eine Abfrage von Fokus und Caret; lange Werte = App antwortet langsam |
| `zoomin.tracking.caret_sources` | Quellen | `process.name`, `zoomin.caret.source` (`system`, `msaa`, `uia`, `estimated`, `cell`) | Caret-Quelle zum ersten Mal in einem Prozess gesehen (dazu Log 2002) |
| `zoomin.tracking.chromium_nudges` | Fenster | – | Chromium-Inhaltsfenster nach IAccessible2 gefragt |
| `zoomin.tracking.read_failures` | Abfragen | `exception.type` | Abfrage von Fokus und Caret mit unerwarteter Ausnahme abgebrochen; Tracking läuft mit Systemcaret weiter (dazu Log 2003, einmal je Ausnahmetyp) |
| `zoomin.tracking.anchor_switches` | Wechsel | `zoomin.anchor.source` (`mouse`, `focus`, `caret`) | Ansicht folgt jetzt Maus, Fokus oder Caret (Render-Thread, ohne Allokation) |
| `zoomin.zoom.temporary_levels` | Änderungen | `zoomin.owner` (`overview`, `window_drag`), `zoomin.action` (`set`, `release`, `move`, `cleared`) | Vorübergehende Zoomstufe (M17) gesetzt, freigegeben, auf einen anderen Monitor verschoben oder durch einen Befehl des Nutzers gelöscht. `set` zählt nur neue Stufen (Ersetzen nicht), ein `move` auf eine eigene Stufe zählt zusätzlich ein `release`. Bleibt ein `set` ohne späteres `release` oder `cleared`, gibt ein Besitzer nicht frei (der KI-Debug-Zustand zeigt dann „temporary“ am Monitor). Abläufe und Logs bringen Übersicht (B2) und Fenster-Verschieben (G1) selbst mit |
| `zoomin.tracking.mouse_push` | Frames | `zoomin.mouse.follow` (`center`, `margin`, `fixpoint`) | Frames, in denen eine Mausbewegung die Ansicht verschoben hat (M17 A1); die Rate je Sekunde zeigt, wie unruhig das Bild im jeweiligen Modus ist. Render-Thread, Instrument und Tags beim Start angelegt, ohne Allokation |
| `zoomin.render.smart_invert_gpu_time` | ms | `zoomin.monitor` | Mittlere Zusatz-GPU-Zeit des intelligenten Invertierens: Umgebungs-Durchgang (Mittel über 120 Läufe) plus Mehrkosten im Magnify-Durchgang (seit M14 je Pipeline gemessen, 32 Frames schlicht gegen intelligent nach 60 Frames Aufwärmen, erneut wenn der ganze Monitor intelligent wird); > 1 ms → Rückfall (Log 3007) |
| `zoomin.render.gpu_time` | ms | `zoomin.monitor`, `zoomin.pass` (`magnify`) | Mittlere GPU-Zeit eines Durchgangs über 120 Frames (M14); Zeitstempel-Abfragen laufen nur, solange jemand das Instrument abonniert (Diagnose, OTLP). Durchgänge überlappen nicht: kein Gesamt-Frame-Wert, weil verschachtelte Disjoint-Abfragen nicht überall erlaubt sind |
| `zoomin.render.ready_time` | ms | `zoomin.monitor` | Prozessstart bis erste fertige Zoom-Pipeline des Monitors (einmal je Prozess und Monitor, nur in der ersten Minute – später angesteckte Monitore zählen nicht, M14) |
| `zoomin.render.smart_invert_frames` | Frames | `zoomin.monitor` | Frames, in denen der Umgebungs-Durchgang lief (ADR-0024); seit M14 nur bei neuem Desktop-Bild und bis die Glättung eingeschwungen ist |
| `zoomin.render.overlay_time` | s | `zoomin.monitor`, `zoomin.state` (`active`, `paused`) | Zeit, in der der Render-Thread arbeitet bzw. schläft – ergibt den Pausenanteil des Filters pro Fenster |
| `zoomin.windowfilter.classifications` | Fenster | `process.name`, `zoomin.result` (`light`, `dark`, `unknown`), `zoomin.caption` (Titelleiste: `light`, `dark`, `unknown`, `none`) | Fenster beim Öffnen eingestuft (dazu Log 2701) |
| `zoomin.windowfilter.look_duration` | ms | – | Ein Blick auf ein Fenster (GDI-Kopie 64×256 und Zählen) |
| `zoomin.windowfilter.update_duration` | ms | – | Fenster auflisten und Bereiche bauen nach einer Fensteränderung |
| `zoomin.windowfilter.regions` | Bereiche | – | Bereiche, die der Shader nach einer Änderung bekommt (höchstens 64) |
| `zoomin.windowfilter.regions_dropped` | Fenster | – | Fenster über dem Limit von 64 Bereichen, die den Monitorfilter zeigen (je Aktualisierung; dazu Log 2705) |
| `zoomin.pointer.fence_blocked` | Bewegungen | – | Mausbewegungen, die der Zaun-Hook verschluckt oder an den Rand geklemmt hat (beobachtbarer Zähler: der Hook zählt nur, ohne Allokation) |
| `zoomin.pointer.fence_changes` | Änderungen | `zoomin.fence.mode`, `zoomin.fence.trigger` (wie oben, dazu `clip_lost`, `hook_lost`, `dispose`) | Zaun gesetzt, neu gesetzt (z. B. nach Win+L) oder gelöst |
| `zoomin.render.still_images` | Bilder | – | Gehaltene Standbilder auf der GPU (M19, Baustein „Standbild“): +1 beim Anlegen, −1 beim Freigeben |
| `zoomin.view.frozen_monitors` | Monitore | – | Gerade eingefrorene Monitore (G2): +1 beim Einfrieren, −1 beim Aufheben (Befehl, Desktopwechsel, Anzeigewechsel, Fehler, Beenden) |

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
| 1401 | Info | KI-Debug allowed: {Allowed} |
| 1402 | Info | KI-Debug command {Tool}: {Result} |
| 1403 | Fehler | KI-Debug command {Tool} failed |
| 1404 | Info | MCP server started |
| 1405 | Info | MCP server ended |
| 1500 | Info | Settings migrated to schema {Version}: mouse follow {Previous} -> {Current} |
| 1501 | Info | Mouse follow mode changed: {Previous} -> {Current} |
| 1502 | Info | Mouse follow hint shown |
| 1520 | Info | Overview on {Monitor} (level before {Level:0.00}) |
| 1521 | Info | Overview ended: {End} (held: {Held}) – `Cancelled` = Zoom-Befehl, Modus- oder Anzeigeänderung |
| 1522 | Fehler | Overview step {Step} failed – `key_signal`, `set_up` (die Übersicht wird dann beendet), `end`, `view_placement`, `release`, `cancel` |
| 1540 | Debug | Window drag ended on {Monitor}: {Kind}, {Action}, zoom {LevelBefore} held at {Level}, crossed monitors {Crossed}, cleared {Cleared} |
| 1541 | Info | Monitors changed during a window drag; its zoom level was given back |
| 1542 | Fehler | Handling a window drag failed |
| 1720 | Info | Monitor {Monitor} frozen at {Level:0.00}x ({Image}); pointer moved to {Target} (G2) |
| 1721 | Info | Freeze lifted on {Count} monitors ({Reason}) – `command`, `display_change`, `desktop_switch`, `session_unlock`, `move_refused` (der Zeiger kam beim Einfrieren zurück und liess sich nicht wieder versetzen), `shortcut_gone` (Kürzel werden bearbeitet oder `Strg+Alt+H` ist nicht registriert), `exit` |
| 1722 | Info | Pointer switched from {From} to {To} (skipped {Skipped} frozen) |
| 1723 | Info | Freezing refused: {Reason} – `layout_unavailable` (Anzeigewechsel läuft), `one_monitor`, `no_pointer`, `overview` (Übersicht läuft auf dem Monitor), `move_refused` |
| 1724 | Info | Monitor switch refused: {Reason} – `layout_unavailable`, `one_monitor`, `no_free_monitor`, `move_refused` |
| 1725 | Fehler | Frozen monitors: {Step} failed; every freeze was lifted and the pointer released – `toggle`, `recompute`, `lift_all`, `exit`, `release` (der Monitorwechsel hebt nichts auf; seine Fehler meldet der Kürzel-Dienst, Log 2202) |
| 2001 | Fehler | UI Automation is unavailable; focus tracking is off |
| 2002 | Info | Caret source in {Process}: {Source} |
| 2003 | Fehler | Reading focus and caret failed; tracking goes on with the system caret |
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
| 2701 | Debug | Window of {Process} measured as {Result} in {DurationMs} ms after {Looks} looks, caption bar {Caption} |
| 2702 | Warnung | Window events unavailable; the filter per window only follows shortcuts and settings |
| 2703 | Fehler | Updating the window regions failed |
| 2704 | Warnung | No bitmap for brightness looks; windows stay unmeasured |
| 2705 | Info | {Dropped} windows over the limit of {Max} regions show the monitor filter |
| 2801 | Info | KI-Debug pipe open |
| 2802 | Info | KI-Debug pipe closed |
| 2803 | Warnung | KI-Debug connection failed |
| 2804 | Info | KI-Debug: ZoomIN not reachable ({Reason}) |
| 2900 | Debug | Window drag started: {Process} {WindowClass} as {Kind} (Programmdatei und Fensterklasse, nie der Titel) |
| 2901 | Debug | Window drag ended ({Reason}) after {DurationMs} ms – `released`, `lost` (Fenster weg, Sicherungs-Timer), `restarted`, `stopped` |
| 2902 | Debug | Window drag kind unclear at the start (hit test {HitTest}); the first change decides |
| 2903 | Debug | Window drag kind from the first change: {Kind} |
| 2904 | Info | Window drag source watches move and size events |
| 2905 | Warnung | Window move and size events unavailable; the zoom stays while windows are dragged |
| 2906 | Fehler | Handling a window drag event failed |
| 2920 | Info | Overview: Esc swallowed {Count} times – beim Ende der Übersicht, nur die Anzahl |
| 2921 | Fehler | Overview key handler failed (im Tastatur-Hook abgefangen) |
| 2922 | Warnung | Screen reader announcement unavailable: {Reason} – `window` oder Ausnahmetyp |
| 2940 | Info | Pointer fence {Mode} after {Trigger} ({Forbidden} forbidden areas, {Filters} filters) |
| 2941 | Warnung | Pointer fence: watching desktop switches failed; the fence is not re-applied after Win+L or UAC |
| 2942 | Warnung | Pointer fence: nothing would be left for the pointer; the pointer stays free |
| 2943 | Fehler | Pointer fence: installing the mouse hook failed; move filters and non-rectangular areas are not kept until it works – einmal bis zum nächsten Erfolg; ein Rechteck hält dann `ClipCursor` allein, der Prüftakt versucht den Hook erneut (Abstand 1, 2, 4 … bis 64 s) |
| 2944 | Warnung | Pointer fence: ClipCursor failed (error {Error}); retried on the next check – einmal bis zum nächsten Erfolg |
| 2945 | Info | Pointer moved to {Monitor} by command ({Reason}) |
| 2946 | Warnung | Moving the pointer ({Reason}) failed (error {Error}) |
| 2947 | Fehler | Pointer fence: {Count} move filter failures; only the forbidden areas applied to those moves – vom Prüftakt gemeldet, nie aus dem Hook |
| 2948 | Info | Pointer fence: another program confines the pointer; not overriding it |
| 2949 | Fehler | Pointer fence update failed ({Trigger}); the pointer is released |
| 3001 | Info | Zoom pipeline ready for {Monitor} |
| 3002 | Info | Display changed on {Monitor}: {Reason} |
| 3003 | Fehler | Zoom pipeline for {Monitor} failed |
| 3004 | Info | Monitors: {Monitors} |
| 3005 | Kritisch | A render thread does not stop; restarting ZoomIN via the watchdog |
| 3006 | Fehler | Render thread without progress for over {Seconds} s; rebuilding the zoom pipeline |
| 3007 | Warnung | Smart inversion on {Monitor} falls back to keep hue: {Reason} ({Milliseconds} ms) – `gpu_feature` oder `budget`, einmal je Pipeline |
| 3050 | Info | Debug snapshot of {Monitor} saved to {Folder} (nur der Ordnername) |
| 3051 | Warnung | Debug snapshot of {Monitor} failed |
| 3100 | Fehler | Ending the overview on {Monitor} before rebuilding the zoom pipeline failed |
| 3300 | Info | Still image of {Width}x{Height} px held |
| 3301 | Info | Still image released |
| 3302 | Warnung | Still image not taken: source format {Format} is not supported |
| 3310 | Info | Frozen view on {Monitor} ({Image}) – `live` oder `still`, beim Beginn eines Einfrierens (G2) |
| 3311 | Info | Frozen view on {Monitor} ended |
| 3312 | Warnung | Still image of frozen monitor {Monitor} not taken or lost with a rebuilt pipeline; it stays live – einmal je Einfrieren und Pipeline |
| 4001 | Warnung | Cannot watch process {ProcessId}: {Reason} |
| 4002 | Fehler | ZoomIN ended unexpectedly (exit code 0x{ExitCode:X8}) |
| 4003 | Warnung | Restarting ZoomIN |
| 4004 | Fehler | Giving up after {Restarts} restarts within {Minutes} minutes |
| 4005 | Info | Cleanup {Step} done |
| 4006 | Fehler | Cleanup {Step} failed |
| 5001 | Info | Setup: ZoomIN {ProcessId} not ended: {Reason} |
| 5002 | Fehler | Setup: registering the installation failed |
| 5003 | Info | Setup: old Inno installation removed |
