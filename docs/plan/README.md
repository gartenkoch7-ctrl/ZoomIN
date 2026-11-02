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

Größen: **S** ≈ wenige Tage · **M** ≈ 1–2 Wochen · **L** ≈ 2–4 Wochen (eine Person, Teilzeit-Schätzung, grob).

## Abhängigkeiten

```
M0 ─► M1 ─► M2 ─┬─► M3 ─┬─► M4 ─┐
                │       └─► M5 ─┼─► M7 ─► M8
                └─────► M6 ─────┘
```
- M3 vor M4/M5: Modi und Filter werden sofort pro Monitor gebaut, nicht nachgerüstet.
- M6 braucht nur M2 (Viewport-Logik) und kann parallel zu M3–M5 laufen.
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
Line View, Auto-Lupe am Fokus, Zoom bis 64×, Installer mit Auto-Update.
