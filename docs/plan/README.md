# Roadmap – MVP (ZoomIN 1.0)

Grundprinzip: **Risiko zuerst.** Die unsicherste Technik (GPU-Overlay, unsichtbar in Teams,
flüssige Bildrate) wird in M1 als Durchstich bewiesen, bevor Features darauf aufbauen.
Jeder Meilenstein endet mit etwas, das man **starten und vorführen** kann.

## Übersicht

| # | Meilenstein | Ergebnis (Demo) | Größe | Status |
|---|---|---|---|---|
| M0 | [Fundament](m0-fundament.md) | Leere App startet, CI grün, Architekturtests aktiv | S | umgesetzt – CI wartet auf GitHub-Remote |
| M1 | [Technischer Durchstich](m1-durchstich.md) | 2× Vollbild-Zoom folgt der Maus, unsichtbar in Teams | L | umgesetzt – Freigabetests, 144 Hz, Win+L offen |
| M2 | [Flüssiger Zoom & Steuerung](m2-zoom-und-steuerung.md) | Stufenloser animierter Zoom 1–16× per Hotkey/Mausrad | M | umgesetzt – Sichttest offen |
| M3 | [Multi-Monitor & Robustheit](m3-multi-monitor.md) | Zoom auf jedem Monitor, übersteht Sperren/Umstecken/Treiber-Reset | L | umgesetzt – Zweitmonitor-Test offen |
| M4 | [Lupe & Angedockt](m4-modi-lupe-angedockt.md) | Drei Zoom-Modi, per Hotkey umschaltbar | M | umgesetzt – Sichttest offen |
| M5 | [Farbfilter & Hervorhebung](m5-filter-und-hervorhebung.md) | Invertieren, Farbschemata, großer Zeiger, Glättung | M | umgesetzt – Sichttest offen |
| M6 | [Fokus- & Caret-Tracking](m6-fokus-und-caret.md) | Zoom folgt Tab-Fokus und Textcursor | L | umgesetzt – App-Matrix offen |
| M7 | [App, Einstellungen & Profile](m7-app-und-einstellungen.md) | Einstellungsfenster, Tray, Profile, Autostart | L | umgesetzt – Sichttest offen |
| M8 | [Release 1.0](m8-release.md) | Getestete, dokumentierte Version zum Download | M | umgesetzt – Repo, Tag, Abschlusstests offen |
| UI | [Neugestaltung & Mauszeiger](ui-neugestaltung.md) | Windows-11-Kartenstil, Übersicht, eigene Mauszeiger-Seite | M | umgesetzt – Sichttest offen |
| M9 | [Setup & Updates](m9-setup-und-updates.md) | Barrierefreies Online-Setup mit UIAccess, Kanälen, Versionswahl und Ein-Klick-Updates | L | geplant – **vor** dem ersten Release |
| M10 | [Sichere Bildschirme](m10-sichere-bildschirme.md) | Zoom auf Anmeldung, Sperre, Strg+Alt+Entf und UAC | L | Design freigegeben – in Arbeit (Spike) |
| M11 | [Diagnose & Telemetrie](m11-diagnose-und-telemetrie.md) | OpenTelemetry lokal: ein Trace über alle Prozesse, Diagnose-Seite, Diagnosepaket | L | Design freigegeben – **vor** dem ersten Release |
| M12 | [Farbfilter pro Fenster](m12-filter-pro-fenster.md) | Helle Fenster automatisch invertiert, dunkle bleiben dunkel; Regeln pro App + Fensterart | L | in `develop`, Sichtprüfung offen (per KI-Debug, M16) – **vor** dem ersten Release |
| M13 | [Intelligentes Invertieren](m13-intelligent-invertieren.md) | Nur helle Flächen invertiert, dunkle Leisten bleiben dunkel, Fotos original – live, ohne Flackern | L | Design freigegeben (ADR-0024), Spike offen – **vor** dem ersten Release |
| M14 | [Interfaces, DI & Performance](m14-interfaces-und-performance.md) | GPU im Leerlauf nahe null, schnellerer Start, Smart Invert sicher im Budget; Dienste statt statischer Klassen, Tests für App/Watchdog/Setup | L | umgesetzt – in `develop` gemergt (2026-11-08, ADR-0025), Testbuild dev.126 geprüft; echte Deinstallation mit den neuen Nahtstellen offen |
| M16 | [KI-Debug (MCP-Server)](m16-ki-debug.md) | Ein KI-Agent liest Zustand, macht Debug-Schnappschüsse und löst Kurzbefehle aus – ohne Klicks | M | umgesetzt – in `develop` gemergt (2026-11-11, ADR-0027); wirkt in der installierten Version erst mit einem neuen Build |

Größen: **S** ≈ wenige Tage · **M** ≈ 1–2 Wochen · **L** ≈ 2–4 Wochen (eine Person, Teilzeit-Schätzung, grob).

## Abhängigkeiten

```
M0 ─► M1 ─► M2 ─┬─► M3 ─┬─► M4 ─┐
                │       └─► M5 ─┼─► M7 ─► M8
                └─────► M6 ─────┘
```
- M9 baut auf M8 auf, der Tag `v1.0.0` wartet aber auf M9: 1.0 erscheint mit dem neuen Setup.
- M11 fasst alle Bereiche an (Log-Umstellung) und kommt vor `v1.0.0`, damit die ersten
  Fehlerberichte vollständig sind. Ab M11 bringt jedes neue Feature seine Telemetrie mit (ADR-0019).
- M3 vor M4/M5: Modi und Filter werden sofort pro Monitor gebaut, nicht nachgerüstet.
- M6 braucht nur M2 (Viewport-Logik) und kann parallel zu M3–M5 laufen.
- M12 baut auf M5 (Filter-Matrizen) und M7 (Einstellungen) auf und kommt vor `v1.0.0`; der
  Spike (Fensterbereiche pro Frame, GPU-Messung, 3-%-Budget) entscheidet über die Umsetzung.
- M13 baut auf M12 (Palette, Bereiche) auf und kommt vor `v1.0.0`; der Spike (Umgebungsbild,
  Glättung, GPU-Zeit ≤ 1 ms Onboard) entscheidet über die Werte.
- M14 baut auf M13 auf. Teil A (Performance) ist vor `v1.0.0` sinnvoll, weil das Smart-Invert-Budget
  knapp ist. Teil B/C (Interfaces und DI nach ADR-0025, Aufteilen, Testprojekte) blockieren 1.0 nicht.
- M7 setzt alle Features voraus, weil es deren Einstellungen bündelt.
  Einfache Einstellungen (Hotkeys, Zoomstufe) werden vorher per JSON-Datei gepflegt.

## Querschnittsregeln für alle Meilensteine
- Jede Aufgabe hält die Regeln aus [AGENTS.md](../../AGENTS.md) ein (300 Zeilen, Schichten,
  Render-Loop ohne Allokationen, Capture-Ausschluss).
- Jeder Meilenstein schließt mit: Akzeptanzkriterien erfüllt, KB aktualisiert,
  Status hier auf **erledigt** gesetzt.
- Neue Erkenntnisse, die einen späteren Plan ändern, werden **sofort** in dessen Datei
  eingetragen (Abschnitt „Offene Fragen / Risiken“).

## Nach dem MVP (nicht geplant, nur gesammelt)
Sprachausgabe (`ZoomIN.Speech`), Input-Transform mit UIAccess + Code-Signing,
Line View, Auto-Lupe am Fokus, Zoom bis 64×. (Installer mit Updates → M9.)
