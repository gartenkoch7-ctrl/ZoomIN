# ADR-0027: KI-Debug über einen MCP-Server (stdio-Rolle + Named Pipe)

- **Status:** Angenommen (Umsetzung in [M16](../plan/m16-ki-debug.md))
- **Datum:** 2026-11-11

## Kontext
Das Overlay ist aus jeder Bildschirmaufnahme ausgenommen (ADR-0002). Ein Agent sieht deshalb nie,
was ZoomIN zeichnet. Für Sichtprüfungen gibt es seit M13 den Debug-Schnappschuss, aber nur über
einen Knopf in „Allgemein → Diagnose“. Die installierte ZoomIN läuft mit UIAccess (ADR-0012), und
Windows (UIPI) blockiert deshalb Klicks von Werkzeugen wie Computer-Use. Ein Agent kann dort
weder den Knopf drücken noch Modus, Filter oder Fensterfilter umschalten. Am 2026-11-11 blieben so
die M12-Sichtprüfungen beim Menschen hängen.

## Entscheidung (Grilling 2026-11-11)
1. **MCP-Server als eigene Rolle:** `ZoomIN.Mcp.exe` (oder `ZoomIN.exe --mcp` im Dev-Build) läuft
   ohne UI wie der Watchdog, mit eigener Telemetrie-Rolle `mcp`. Claude Code (oder ein anderer
   MCP-Client) startet sie über stdio. Paket: offizielles MCP C# SDK (`ModelContextProtocol.Core`,
   Apache-2.0) – nur der Kern, ohne Hosting und ohne ASP.NET.
   - **Warum eine zweite Startdatei** (Review 2026-11-11): Die installierte `ZoomIN.exe` hat das
     UIAccess-Manifest. `CreateProcess` startet sie nicht (`ERROR_ELEVATION_REQUIRED`, ADR-0012),
     und `ShellExecute` kann stdin/stdout nicht umleiten. `ZoomIN.Mcp.exe` ist eine Kopie des
     App-Hosts ohne dieses Manifest. Sie lädt dieselbe `ZoomIN.dll` und startet immer nur die
     MCP-Rolle. `publish.ps1` erzeugt sie, der Dev-Build legt sie beim Bauen ab.
2. **Named Pipe zur laufenden Instanz:** Die Rolle reicht jeden Tool-Aufruf über eine Pipe an die
   Tray-App weiter (`ZoomIN.AgentDebug.<Sitzung>`, `PipeOptions.CurrentUserOnly`). Es gibt keinen
   Netzwerkport. Rahmen: 4 Byte Länge + UTF-8-JSON, mit Protokollversion.
3. **Freigabe:** Schalter „KI-Debug erlauben“ in „Allgemein → Diagnose“, ab Werk aus. Er bleibt an,
   bis der Nutzer ihn ausschaltet (Entscheidung des Nutzers). Die Pipe existiert nur, solange er an
   ist. Jeder Befehl wird protokolliert, ohne Inhalte.
4. **Umfang:** Zustand lesen, Debug-Schnappschuss (Bilder direkt im Tool-Ergebnis, wahlweise
   Ausschnitt und Verkleinerung) und die Kurzbefehle der App (Zoom, Modus, Monitorfilter,
   Filter für das aktive Fenster, Fadenkreuz, Verfolgung). Nichts, was Dateien des Nutzers,
   Einstellungen außerhalb dieser Befehle, Updates oder Deinstallation berührt.
5. **Nur die Tray-App:** Die Instanz auf sicheren Bildschirmen (ADR-0016) bietet keine Pipe an.

## Begründung
- stdio + Pipe öffnet keinen Port. Die Pipe lässt nur denselben Benutzer zu.
- Die Bilder kommen über die Pipe statt über AppData. Der Agent hat AppData schon veraltet oder
  umgeleitet gesehen (Sandbox), die Pipe umgeht das.
- Die Rolle im selben Exe braucht keine zweite Installation und passt immer zur Version der App.

## Folgen
- Mit eingeschaltetem Schalter kann jedes Programm des Benutzers den Bildschirminhalt lesen, den
  ZoomIN zeichnet. Der Schalter erklärt das, und das Protokoll zeigt jeden Zugriff.
- Neues Paket `ModelContextProtocol.Core` (und dessen Abhängigkeit `Microsoft.Extensions.AI.Abstractions`).
- `.mcp.json` im Repo zeigt auf `ZoomIN.Mcp.exe` der installierten ZoomIN. Für Dev-Builds trägt
  man den Pfad lokal ein.
- Die SignPath-Artefaktkonfiguration (`docs/plan/signpath-antrag.md`) muss `ZoomIN.Mcp.exe` mit
  signieren. Unsigniert startet sie trotzdem, sie braucht kein UIAccess.
