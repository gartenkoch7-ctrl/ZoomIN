# ADR-0004: Einstellungen als JSON in %AppData%

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Entscheidung
Einstellungen und Profile liegen als JSON unter `%AppData%\ZoomIN\` (`settings.json`,
`profiles\*.json`), serialisiert mit `System.Text.Json` + Source Generator.

## Begründung
Lesbar, leicht zu sichern und zu teilen, versionierbar; keine Registry-Abhängigkeit.

## Konsequenzen
- Jede Datei enthält `"schemaVersion"`; Migrationen liegen in `Core/Settings/Migrations/`.
- Schreiben atomar (temporäre Datei + `File.Replace`), damit ein Absturz nichts zerstört.
- Ungültige Datei → Backup anlegen, mit Standardwerten starten, Nutzer informieren.
