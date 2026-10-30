# Mitmachen bei ZoomIN

Danke für dein Interesse! Bitte lies zuerst **[AGENTS.md](AGENTS.md)** – dort stehen die
verbindlichen Regeln für Menschen und KI-Agents.

## Das Wichtigste in Kürze
- **Clean-Room:** ZoomIN ist von SuperNova inspiriert, aber eigenständig. Nichts dekompilieren,
  keine fremden Texte, Grafiken oder Farbschemata übernehmen (AGENTS.md, Abschnitt 3).
- **Max. 300 Zeilen pro Code- und Testdatei** – geprüft per Pre-Commit-Hook und CI.
- **Schichten:** Core kennt niemanden; Platform und Rendering kennen nur Core; App verbindet.
  Architekturtests prüfen das automatisch.
- **Render-Loop ohne Allokationen** – Tests mit `GC.GetAllocatedBytesForCurrentThread` erzwingen 0 B.
- Code und Commits auf Englisch (Conventional Commits), Doku auf Deutsch.

## Einrichten
```powershell
git config core.hooksPath .githooks
dotnet build ZoomIN.slnx
dotnet test --solution ZoomIN.slnx
dotnet run --project src/ZoomIN.App
```

## Wo finde ich was?
| Thema | Ort |
|---|---|
| Wissen, Entscheidungen | [docs/kb/](docs/kb/README.md), [docs/adr/](docs/adr/) |
| Roadmap, Meilensteine | [docs/plan/](docs/plan/README.md) |
| Testumgebung, was ungetestet ist | [docs/kb/testumgebung.md](docs/kb/testumgebung.md) |

## Fehler melden
- Hardware-Probleme: Issue-Vorlage **„Hardware-Bericht“** – mit Log
  (Einstellungen → Info & Status → Log-Ordner öffnen).
- Bitte keine Screenshots von SuperNova anhängen (Clean-Room).
