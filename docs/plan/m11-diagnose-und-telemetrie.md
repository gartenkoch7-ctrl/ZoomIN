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
- [ ] Pakete (Begründung im PR): `Microsoft.Extensions.Logging.Abstractions` (Core),
      `OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` (nur App, nachgeladen)
- [ ] Core: `Diagnostics/Telemetry` – eine `ActivitySource` und ein `Meter` „ZoomIN“, Namensschema
      `zoomin.<feature>.<vorgang>`; Modell `TelemetryOptions` (Stufe, Schalter, Log-Stufe, Tage,
      Adresse, „erhöht bis“) mit Logik für Stufen → Filter und das Zurückfallen nach 7 Tagen + Tests
- [ ] Platform: Datei-Senken – Log-Tagesdatei (TraceId/SpanId je Zeile) und Ablauf-Datei
      (JSON-Zeilen), gepuffert auf eigenem Thread, 20 MB/Tag, Aufräumen beim Start und täglich
- [ ] App: gemeinsamer Telemetrie-Start für **jede** Rolle (app, watchdog, update, elevated,
      uninstall, secure, setup) mit `zoomin.role`; Stufe wird zur Laufzeit gewechselt
- [ ] Trace-Kontext weitergeben, wo ZoomIN sich selbst startet (Watchdog, Updater, Hilfsprozess,
      Setup → App): `TRACEPARENT` setzen und beim Start übernehmen
- [ ] Sicherer Modus: nur Datei-Senken, Stufe aus der Einstellungskopie in SYSTEMs Profil, kein Export
- [ ] `ILog`/`FileLog` durch `ILogger<T>` ersetzen (alle ~87 Stellen), EventIds je Feature-Bereich
      vergeben; `ILog`, `NullLog`, `FileLog` entfernen
- [ ] AGENTS.md: Abschnitt „Telemetrie“ (Pflicht, Datenschutz, Render-Loop) + Punkt in der
      Definition of Done; Katalog `docs/kb/telemetrie.md` anlegen
- [ ] Architekturtests: nur Quelle/Meter „ZoomIN“, Namensschema, eindeutige EventIds, kein
      `Console.Write*`/`Debug.Write*`, nur Core-Abstraktionen in Core; Datenschutz-Test gegen
      verbotene Attribut-Namen (`title`, `name`, `text`, `path` …)
- [ ] Startzeit vorher/nachher messen (Log „erstes Bild nach … ms“, normal und sicherer Modus)

### 2 · Abläufe und Metriken je Bereich (je ein eigener Commit)
- [ ] **Start, Monitore, Pipeline:** Span App-Start → erstes Bild; Anzeigeänderung; Pipeline
      starten/neu starten/Gerät verloren. Metriken pro Monitor: Frame-Zeit (Histogramm), verpasste
      Frames, Neustarts. Benchmark `0 B` mit Listener
- [ ] **Fokus & Caret:** Zähler je Prozess und Quelle (System/MSAA/UIA/geschätzt/keine), Dauer pro
      Abfrage, Chromium-Anstoß, Ankerwechsel Maus/Fokus/Caret; Log bei Quellenwechsel je Prozess
- [ ] **Setup & Updates:** ein Trace über Prüfen, Herunterladen, Signatur, Installieren,
      Wiederherstellen; Fehler mit Schritt und Ursache
- [ ] **Sichere Bildschirme & Profile:** Start bis erstes Bild, Desktopwechsel, Einstellungskopie;
      Profilwechsel, Einstellungen laden/speichern
- [ ] Katalog `docs/kb/telemetrie.md` je Bereich ergänzen

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
