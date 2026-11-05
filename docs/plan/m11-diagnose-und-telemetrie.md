# M11 – Diagnose & Telemetrie

**Ziel:** Jeder Ablauf in ZoomIN ist nachvollziehbar – welche Schritte liefen, wie lange sie
dauerten und wo etwas scheiterte –, ohne dass Daten den PC verlassen oder Inhalte des Nutzers
aufgezeichnet werden. Jedes künftige Feature bringt seine Telemetrie gleich mit.
**Demo:** Update auslösen → im lokalen Aspire Dashboard erscheint **ein** Trace über Setup,
Updater, Hilfsprozess und App mit allen Schritten. In Teams tippen → Log zeigt, welche Caret-Quelle
antwortet. Auf „Diagnose“ → „Diagnosepaket erstellen“ → ZIP mit Logs und Abläufen des Tages.
**Voraussetzungen:** Merge von `feat/tracking-compat`. Vor dem Tag `v1.0.0`.
→ [ADR-0019](../adr/0019-telemetrie-lokal-mit-opentelemetry.md)

## Entscheidungen (Grilling 2026-11-05)
| Thema | Entscheidung |
|---|---|
| Ziel der Daten | Nur lokal; OTLP-Export nur mit eingetragener Adresse (Warnhinweis außerhalb des PCs) |
| Zweck | Fehlersuche, Abläufe, Leistung; Nutzung nur als lokale Zähler |
| Log-API | `ILogger<T>` + `[LoggerMessage]` statt `ILog` (alle Stellen) |
| Core | nur `ActivitySource`/`Meter` (.NET) und `Microsoft.Extensions.Logging.Abstractions` |
| Render-Loop | nur Metriken, Instrumente/Tags beim Start angelegt, Benchmark `0 B` mit Listener |
| Datenschutz | Prozessname, Fensterklasse, Steuerelementtyp, Rechtecke ja; Titel, Namen, Text, Nutzerpfade nie |
| Lokal | Log-Tagesdatei mit TraceId/SpanId + Abläufe als JSON-Zeilen |
| Aufbewahrung | 1 Tag, auf der Diagnose-Seite bis 7 Tage; max. 20 MB pro Tag |
| Stufen | Nur Fehler · **Normal** (Logs ab Warnung + Abläufe) · Ausführlich (alles) · Benutzerdefiniert |
| Fehler | werden in jeder Stufe aufgezeichnet |
| Zurückfallen | Stufe und Tage fallen nach 7 Tagen auf Normal / 1 Tag zurück („Erhöht bis …“) |
| Prozesse | ein Trace über alle Rollen (`zoomin.role`), W3C `TRACEPARENT` per Umgebungsvariable |
| Sicherer Bildschirm | schreibt nur lokal ins SYSTEM-Profil, exportiert nie |
| Bedienung | Karte „Diagnose“ unter „Allgemein“ + Standard-Variablen `OTEL_*` |
| Startzeit | ≤ +20 ms bis zum ersten Bild; sicherer Bildschirm ohne messbare Verschlechterung |
| Pflicht | AGENTS.md-Regel, Definition of Done, Katalog `docs/kb/telemetrie.md`, Architekturtests |

## Aufgaben
### 1 · Fundament
- [x] Pakete: `Microsoft.Extensions.Logging.Abstractions` 10.0.14 (Core; direkt auch in App,
      Rendering, Watchdog, Setup, damit der `[LoggerMessage]`-Generator läuft), `Microsoft.Extensions.Logging`,
      `OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1 (Platform; Apache-2.0/MIT)
- [x] Core: `Telemetry` (Quelle/Meter „ZoomIN“, `FlushBeforeExit`), `TelemetryRole`, `TelemetryLevel`,
      `TelemetryOptions`/`TelemetryFilter` (Stufen, Fehler immer, Zurückfallen nach 7 Tagen),
      `TraceParentArgument`, `TelemetryPrivacy`, Einstellungen `Telemetry*` + Sanitizer + Tests
- [x] Platform: `TelemetryFiles` (**stündliche** Dateien statt Tagesdatei, damit „1 Tag“ höchstens
      24 h heißt; 20 MB/Tag, Aufräumen beim Start und stündlich), `LogFileExporter`, `TraceFileExporter`
- [x] Gemeinsamer Start für jede Rolle (`TelemetryHost`, `Program.Main` in App und Setup); Stufe
      und Export-Adresse wechseln ohne Neustart (`SwitchableLoggerProvider`)
- [x] Trace-Kontext per `--traceparent` an allen Selbststarts (Watchdog, Neustart nach Update,
      Hilfsprozesse mit runas, Deinstallation, Setup ↔ App); `TRACEPARENT` als Rückfall
- [x] Sicherer Modus: nur Dateien im SYSTEM-Profil, Stufe aus der geprüften Einstellung, Adresse
      vom Sanitizer geleert und vom Host ignoriert
- [x] Alle 85 Log-Stellen auf `[LoggerMessage]` mit EventIds je Bereich; `ILog`, `NullLog`,
      `FileLog` entfernt. Dabei 5 Meldungen mit vollen Pfaden (Benutzername) auf Datei-/Ordnernamen gekürzt
- [x] AGENTS.md 2.5 + Definition of Done; Katalog `docs/kb/telemetrie.md`
- [x] `TelemetryConventionTests`: Quelle/Meter nur in Core, Namensschema, EventIds eindeutig und
      im Projektbereich, Attribut-Schlüssel und Log-Platzhalter ohne Nutzerinhalte, keine Konsolenausgabe
- [x] Startzeit gemessen: zuerst ~90 ms auf dem Startpfad (Budget 20 ms) → Pipelines werden jetzt
      im Hintergrund gebaut, Frühes wird gepuffert und nachgeschrieben (`EarlyActivities`,
      `ReplayContextProcessor`): **~4 ms Host-Start, ~9 ms inkl. erster Logzeile und Operation**
      ([performance.md](../kb/performance.md))
- [x] Test mit echter App 2026-11-05 (Release, warm): Overlay nach 419–437 ms mit M11 gegenüber
      451–483 ms ohne – kein Nachteil; `zoomin.app.start` 309–312 ms; Watchdog bekommt
      `--traceparent` mit der Trace-Id des App-Starts; 118 Logzeilen ohne Fehler und ohne
      Benutzernamen; alte Tageslogs wie vorgesehen aufgeräumt
- [ ] Sicherer Bildschirm (`zoomin.secure.start` auf Sperre/UAC) – braucht Test am Anmeldebildschirm
- [ ] Einstellungsänderung zur Laufzeit an den Host weitergeben (kommt mit der Diagnose-Seite, Schritt 3)

### 2 · Abläufe und Metriken je Bereich (je ein eigener Commit)
- [x] **Start, Monitore, Pipeline:** `zoomin.render.pipeline_start`/`rebuild`; Messwerte je Monitor
      Frame-Abstand, verpasste Frames, Zeit bis erstes Bild, Neustarts mit Grund. 0 B mit dem
      OpenTelemetry-SDK als Zuhörer (`RenderMetricsTests`). „App-Start → erstes Bild“ ist beim
      normalen Start ohne Zoom kein Ablauf; stattdessen `zoomin.render.show_latency` bei jedem Einschalten
- [x] **Fokus & Caret:** Caret-Quelle je Prozess (Log 2002 einmal pro neuer Quelle – Notepad
      wechselt im Blinktakt), Dauer je Abfrage, Chromium-Anstoß, Ankerwechsel (Render-Thread, 0 B)
- [x] **Setup & Updates:** ein Trace Prüfen → Download → Sicherung → Adminschritt → Anwenden →
      Signatur → Neustart; jeder Schritt mit Version und Ergebnis, Fehler markiert (`UpdateTelemetry`).
      „Wiederherstellen“ ist Teil von `zoomin.update.apply` (Ergebnis + Status)
- [x] **Sichere Bildschirme & Profile:** Herkunft der Einstellungen im sicheren Modus,
      Einstellungen laden/speichern, Profilwechsel (ohne Profilnamen). Der Desktopwechsel ist ein
      Neustart des sicheren Modus und damit `zoomin.secure.start`
- [x] Katalog `docs/kb/telemetrie.md` je Bereich ergänzt
- [ ] Sichttest: einmal „Ausführlich“ + lokaler Collector (Aspire Dashboard) und Update-Trace ansehen

### 3 · Diagnose-Seite und Diagnosepaket
- [ ] Karte „Diagnose“ unter „Allgemein“: Stufen-Auswahl, bei „Benutzerdefiniert“ Schalter
      Logs/Abläufe/Metriken + Log-Stufe, Hinweis „Fehler werden immer aufgezeichnet“,
      Aufbewahrung 1–7 Tage, „Erhöht bis …“, OTLP-Adresse mit Warnhinweis außerhalb des PCs
- [ ] Knopf „Diagnosepaket erstellen“: ZIP aus Logs und Abläufen der aufbewahrten Tage plus
      Versions-/Systeminfo; Ordner öffnen
- [ ] Texte Deutsch/Englisch, vollständig per Tastatur und Bildschirmleser bedienbar

## Akzeptanzkriterien
- [ ] Ohne eingetragene Adresse verlässt kein Byte den PC (Netzwerkmitschnitt beim Start/Update)
- [ ] Ein Update erscheint im Aspire Dashboard als **ein** Trace über alle beteiligten Prozesse
- [ ] Stufe „Nur Fehler“ schreibt keine Warnung/Info; Fehler erscheinen in jeder Stufe
- [ ] Stufe wechselt ohne Neustart; Erhöhung fällt nach 7 Tagen auf Normal / 1 Tag zurück (Test)
- [ ] Aufbewahrung und 20-MB-Grenze greifen, auch im SYSTEM-Profil
- [ ] Render-Loop-Benchmark mit Listener: `0 B`
- [ ] Startzeit ≤ +20 ms; sicherer Bildschirm ohne messbare Verschlechterung
- [ ] Keine Fenstertitel, Texte oder Nutzerpfade in einem Diagnosepaket nach einem Tag Nutzung
      (Stichprobe + Datenschutz-Test)
- [ ] Architekturtests grün; Katalog vollständig

## Offene Fragen / Risiken
- Umfang: fasst fast jede Datei an; Schritt 2 strikt pro Bereich committen.
- 1.0 verschiebt sich um diesen Meilenstein.
- Datenschutz-Test findet nur bekannte Feldnamen; Freitext in Lognachrichten im Review prüfen.
- Läuft das OpenTelemetry-SDK im SYSTEM-Prozess ohne WinUI und ohne Netzwerk schnell genug?
- 1 Tag Aufbewahrung: später bemerkte Fehler sind nicht mehr nachvollziehbar (bewusst).
