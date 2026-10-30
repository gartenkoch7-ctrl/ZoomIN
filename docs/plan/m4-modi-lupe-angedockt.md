# M4 – Zoom-Modi: Lupe & Angedockt

**Ziel:** Neben Vollbild zwei weitere Modi über eine gemeinsame Abstraktion.
**Demo:** `Strg+Alt+F` / `L` / `D` springen direkt in Vollbild / Lupe / Angedockt.
**Voraussetzungen:** M3.

## Entscheidungen (Grilling 2026-10-30)
- Lupe: **Rechteck**, Standard **40 % × 30 %** des Monitors, per `Strg+Alt+Umschalt+Pfeile`
  in 5-%-Schritten (10–90 %); rund erst nach MVP
- Angedockt: Standard **oben, 40 %**, auf dem **Monitor der Maus** beim Einschalten; Platz wird
  per **AppBar reserviert**; der Streifen ist reine Anzeige
- **Ein Hotkey je Modus** (`Strg+Alt+F/L/D`): wechselt in den Modus **und schaltet ein**; derselbe
  Hotkey im aktiven Modus schaltet aus; `Strg+Alt+Z` nutzt den zuletzt benutzten Modus
- **Eigene Zoomstufe je Modus** (weiterhin pro Monitor): Start Vollbild 2×, Lupe/Angedockt 3×
- Rahmen um Lupe und Streifen: 4 px Bernstein (Farbe einstellbar ab M5/M7)

## Aufgaben

### 1. Core (`Core/Modes/`, `Core/Zoom/`)
- [x] `ZoomMode`, `DockEdge`, `LensSize`, `DockLayout`, `ModeFrame`
- [x] `ModeGeometry`: Vollbild, Lupe (Fixpunkt Maus, im Monitor gehalten), Angedockt
      (Quelle nur aus dem Restbereich, nie aus dem Streifen), `DockRect`/`RestRect`
- [x] `ZoomLevels` (Stufe je Modus, geteilt/pro Monitor), `ZoomModel` mit `Activate`, `ResizeLens`
- [x] Tests: Geometrie inkl. Fixpunkt-Beweis für die Lupe, Modell inkl. Modi und Andocken, 0 B

### 2. Rendering
- [x] Shader mit optionalem Rahmen (Viewport = Ziel + Rahmen), Rest transparent (Clear)
- [x] `RenderSession` zeichnet den Modus-Frame; Vollbild unverändert ohne Clear/Rahmen
- [x] WARP-Tests: transparenter Rest, Rahmenfarbe, vergrößerter Inhalt

### 3. Angedockt (`Platform/Shell/AppBarReservation`)
- [x] AppBar auf eigenem Thread mit Nachrichtenschleife, transparentes Fenster
- [x] `ABM_QUERYPOS`/`ABM_SETPOS`, Dicke bleibt bei Verschiebung durch andere AppBars
- [x] Fenster-Handle in `%LocalAppData%\ZoomIN\appbar.hwnd`; Watchdog-Schritt `AppBarCleanup`
- [x] `DockedStripSync` (App) reserviert/gibt frei passend zum Modell

### 4. App
- [x] Hotkeys F/L/D und Lupengröße, Einstellungen (Modus, Stufen je Modus, Lupe, Andocken)
- [x] Statusfenster zeigt Modus und Stufen

## Akzeptanzkriterien
- [x] Modus-Hotkeys schalten an/aus und wechseln (automatisch geprüft)
- [x] Angedockt: Arbeitsbereich oben um 40 % verkleinert (`0,432–1920,1032`), danach zurück
- [x] Nach Beenden **und** nach Absturz ist der AppBar-Platz wieder frei (automatisch geprüft)
- [x] Klicks treffen in der Lupe (Fixpunkt per Test bewiesen)
- [ ] Sichttest: Lupe/Rahmen/Streifen sehen richtig aus, Wechsel ohne Flackern
- [ ] Alle Modi bei Bildschirmfreigabe unsichtbar (inkl. Rahmen) – mit M1-Freigabetest
- [ ] Mehrere Monitore (mit virtuellem Monitor aus M3)

## Offene Fragen / Risiken
- Ist die Taskleiste oben, verschiebt die Shell den Streifen nach unten; die Overlay-Geometrie
  rechnet noch mit dem Monitorrand → bei Bedarf das von der Shell gemeldete Rechteck übernehmen.
- Umschalten Lupe ↔ Vollbild wechselt die Geometrie sofort, nur die Stufe gleitet.
