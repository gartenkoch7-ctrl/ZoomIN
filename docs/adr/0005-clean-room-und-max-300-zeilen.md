# ADR-0005: Clean-Room-Entwicklung und max. 300 Zeilen pro Datei

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Clean-Room
SuperNova ist kommerziell; dessen Lizenz verbietet sehr wahrscheinlich Reverse Engineering
der Binaries. Wir analysieren **nur** als Black Box (Benutzung, öffentliche Doku) und bauen
eigenständig nach. Regeln: AGENTS.md, Abschnitt 3.

## 300-Zeilen-Limit
Code- und Testdateien haben höchstens 300 Zeilen.

**Begründung:** Kleine Dateien zwingen zu klaren Verantwortlichkeiten, sind für Menschen und
KI-Agents vollständig im Kontext lesbar und reduzieren Merge-Konflikte.

**Durchsetzung:** `scripts/check-file-length.ps1` im Pre-Commit-Hook (`.githooks/`) und in
CI (`.github/workflows/ci.yml`). Generierte Dateien sind ausgenommen.
