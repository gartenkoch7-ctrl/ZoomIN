# M16 · KI-Debug (MCP-Server)

**Ziel:** Ein KI-Agent kann ZoomIN selbst prüfen. Er liest den Zustand, macht Debug-Schnappschüsse
und löst die Kurzbefehle aus, ohne dass ein Mensch klicken muss. Entscheidung: [ADR-0027](../adr/0027-ki-debug-mcp.md).

**Auslöser:** Die M12-Sichtprüfungen (2026-11-11) scheiterten daran, dass die installierte ZoomIN
wegen UIAccess keine Klicks von Computer-Use annimmt.

## Aufgaben
### 1 · Core
- [x] Protokoll: `AgentRequest`/`AgentResponse`/`AgentImage`, Versionsnummer, JSON-Source-Generator
- [x] Werkzeugkatalog (Namen, Beschreibungen, Kurzbefehle) als reine Tabelle
- [x] Setting `AgentDebugEnabled` (ab Werk aus), nicht in Profilen
- [x] PNG lesen/ausschneiden/verkleinern für die eigenen Schnappschüsse (reine Logik)

### 2 · Platform
- [x] Pipe-Server und -Client mit `CurrentUserOnly`, Rahmen 4 Byte Länge + JSON, Größenlimit

### 3 · App
- [x] `AgentDebugHost`: Pipe an/aus mit dem Schalter, nur in der Tray-App
- [x] `AgentCommandHandler`: Zustand, Schnappschuss (Bilder über die Pipe), Kurzbefehle
- [x] Rolle `--mcp` mit `ModelContextProtocol.Core` über stdio
- [x] Schalter „KI-Debug erlauben“ in „Allgemein → Diagnose“, Texte Deutsch/Englisch
- [x] `.mcp.json` im Repo

### 4 · Qualität
- [x] Telemetrie: Log je Befehl (ohne Inhalte), Ablauf `zoomin.agent.command`, Katalog
- [x] Unit-Tests: Protokoll-Rundreise, Rahmen, PNG-Ausschnitt, Befehlszuordnung, Pipe Ende-zu-Ende
- [x] KB: `testumgebung.md` (wie ein Agent prüft), `telemetrie.md`

## Akzeptanzkriterien
- [x] Schalter aus → keine Pipe; das MCP-Tool meldet verständlich, wie man es einschaltet
- [x] `take_snapshot` liefert das Ergebnisbild aller Monitore direkt im Tool-Ergebnis
- [x] Kurzbefehle wirken wie die Tastenkürzel
- [x] Die M12-Sichtprüfungen lassen sich damit ohne Menschen fahren (2026-11-11, bis auf die Titelleisten-Wahl auf der Detailseite)

## Stand 2026-11-11
- Ende-zu-Ende am Dev-Build geprüft: `get_state`, `take_snapshot` (alle Bildarten, `crop`,
  `max_width`) und `run_shortcut` über `scripts/agent-debug.ps1`, auch aus der Agenten-Sandbox.
- Der Schalter übersteht den Neustart, die Pipe öffnet sich danach von selbst.
- Für die installierte Version braucht es einen neuen Build (Installer). Erst dann zeigt die
  `.mcp.json` auf eine ZoomIN, die `--mcp` kennt.
