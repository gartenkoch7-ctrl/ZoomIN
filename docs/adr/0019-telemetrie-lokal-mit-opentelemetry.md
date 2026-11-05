# ADR-0019: Telemetrie mit OpenTelemetry – lokal, opt-in exportierbar

- **Status:** Angenommen
- **Datum:** 2026-11-05

## Kontext
ZoomIN loggt bisher über die eigene Schnittstelle `ILog` (Info/Warn/Error) in Tagesdateien unter
`%LocalAppData%\ZoomIN\logs` (`FileLog`, ~87 Aufrufstellen, jede Rolle legt ein eigenes `FileLog`
an). Es gibt nur Text, keine Abläufe und keine Messwerte. Die Fehlersuche am 2026-11-05
(Caret-Kette) brauchte ein eigenes Messwerkzeug, weil im Log nicht stand, welche Quelle in welcher
App antwortet. Ein Update läuft über drei bis vier Prozesse (Setup, Updater, Hilfsprozess mit
Adminrechten, App), die sich im Log nicht zuordnen lassen.

Gleichzeitig ist ZoomIN ein Werkzeug für sehbehinderte Menschen, das sich bewusst vor
Bildschirmübertragungen versteckt. Die Fokus- und Caret-Verfolgung sieht Fenstertitel, Texte und
Dokumentnamen. Telemetrie darf daraus kein Datenleck machen.

## Entscheidung
1. **OpenTelemetry** für Logs, Abläufe (Traces) und Metriken.
   - Core nutzt nur `System.Diagnostics.ActivitySource`/`Meter` (Teil von .NET) und
     `Microsoft.Extensions.Logging.Abstractions` (`ILogger<T>`, keine IO). Das ist die einzige neue
     Abhängigkeit von Core; sie ändert die Schichtregel „Core kennt niemanden“ nicht, weil sie
     nur Schnittstellen enthält.
   - Das OpenTelemetry-SDK, die Datei-Senken und der OTLP-Exporter liegen in Platform/App
     (Composition Root). Der OTLP-Exporter wird nur geladen, wenn eine Adresse eingetragen ist.
   - `ILog`/`FileLog` werden vollständig durch `ILogger<T>` mit `[LoggerMessage]`-Source-Generator
     ersetzt (strukturierte Felder, keine Allokation bei abgeschalteter Stufe).
2. **Nur lokal.** Standardmäßig verlässt nichts den PC. Export per OTLP nur, wenn der Nutzer auf der
   Seite „Diagnose“ eine Adresse einträgt (bei Adressen außerhalb des PCs mit Warnhinweis) oder
   ein Entwickler die Standard-Variablen `OTEL_*` setzt. Der Prozess auf sicheren Bildschirmen
   (SYSTEM) exportiert **nie**.
3. **Keine Inhalte.** Erlaubt sind Prozessname, Fensterklasse, UIA-Steuerelementtyp, Rechtecke,
   Monitor- und Versionsdaten, Fehlertexte. Verboten in Logs und Attributen: Fenstertitel,
   Elementnamen, eingegebener oder angezeigter Text, Dateipfade aus Nutzerdaten.
4. **Lokale Speicherung:** Logs als Tagesdatei mit TraceId/SpanId; abgeschlossene Abläufe als
   JSON-Zeilen in einer eigenen Datei. Aufbewahrung standardmäßig **1 Tag**, auf der Diagnose-Seite
   bis **7 Tage** erhöhbar; höchstens **20 MB pro Tag**.
5. **Aufzeichnungsstufen:** „Nur Fehler“, „Normal“ (Standard: Logs ab Warnung + Abläufe),
   „Ausführlich“ (alle Logs, Abläufe, Metriken), „Benutzerdefiniert“ (Schalter je Art + Log-Stufe).
   Fehler und Abstürze werden **immer** aufgezeichnet. Eine Erhöhung (Stufe oder Tage) fällt nach
   7 Tagen automatisch auf „Normal“ / 1 Tag zurück. Die Stufe gilt für Datei und Export gleich und
   wirkt ohne Neustart.
6. **Render-Loop:** nur Metriken über beim Start angelegte Instrumente und Tags; keine Logs, keine
   Spans pro Frame. Benchmark mit angehängtem Listener (Stufe „Ausführlich“) muss `0 B` zeigen.
7. **Ein Trace über Prozesse.** Jede Rolle startet die Telemetrie gleich und setzt `zoomin.role`
   (app, watchdog, update, elevated, uninstall, secure, setup). Startet ein ZoomIN-Prozess einen
   anderen, gibt er den W3C-Kontext (`TRACEPARENT`) per Umgebungsvariable weiter.
8. **Pflicht für jedes Feature:** eine `ActivitySource` und ein `Meter` namens „ZoomIN“,
   Namensschema `zoomin.<feature>.<vorgang>`, eindeutige Log-EventIds, alles im Katalog
   `docs/kb/telemetrie.md`. Durchgesetzt über AGENTS.md, Definition of Done und Architekturtests.
9. **Startzeit:** höchstens +20 ms bis zum ersten Bild; auf sicheren Bildschirmen keine messbare
   Verschlechterung.

## Verworfene Alternativen
- **Daten an einen eigenen Server (opt-in):** braucht Server, Datenschutzerklärung,
  Zustimmungsdialog und Anonymisierung – zu viel Risiko für ein Werkzeug, das Privatsphäre
  verspricht.
- **`ILog` behalten und OpenTelemetry dahinter:** nur Text ohne Felder, String-Allokation bei jedem
  Aufruf.
- **Spans pro Frame:** Activity-Objekte allozieren; bricht die 0-B-Regel.
- **Systemweit alles abschaltbar inkl. Fehler:** nach einem Absturz gäbe es keinen Hinweis.

## Konsequenzen
- ✅ Fehlerberichte enthalten den kompletten Ablauf über alle Prozesse (Diagnosepaket).
- ✅ Entwickler sehen Traces und Metriken live in einem lokalen Collector (z. B. Aspire Dashboard,
  Jaeger), ohne dass Endnutzer etwas davon merken.
- ⚠️ Großer Umbau: alle Log-Stellen und jede Rolle werden angefasst (eigener Meilenstein M11).
- ⚠️ Der Datenschutz-Test erkennt nur bekannte Feldnamen; Freitext in Lognachrichten braucht Review.
- ⚠️ Mit 1 Tag Aufbewahrung fehlen Daten, wenn ein Fehler erst später bemerkt wird – bewusster
  Preis für Datenschutz und Platz.
- ⚠️ Download wird etwas größer (OpenTelemetry-SDK, OTLP-Exporter; Apache-2.0/MIT).
