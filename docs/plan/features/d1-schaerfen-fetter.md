# D1 · Schärfen und Fetter
**Meilenstein:** Spike parallel zu [M17](../m17-ruhige-ansicht.md), Umsetzung in [M18](../m18-lesbarkeit.md) · **Spur:** M17-D (Spike), M18-A (Umsetzung) · **Branch:** `spike/d1-schaerfen-fetter` (von `develop`), danach `feat/m18-d1-schaerfen-fetter` · **Größe:** Spike S, Umsetzung M · **Status:** geplant

**Ziel:** Dünne Schrift bleibt bei hoher Stufe lesbar. „Schärfe“ hebt Kanten an, „Fetter“ verbreitert
dunkle Striche, beide nach der Vergrößerung. Vorher zeigt ein Spike, ob das sauber und im
GPU-Budget geht.
**Demo:** Vollbild 8× auf einem Word-Dokument in 9 pt: Schärfe 60 %, Fett 1 px → Buchstaben
klarer und kräftiger. Dasselbe mit „Invertiert (intelligent)“ ohne Halos.
**Voraussetzungen:** Spike: keine (startet sofort mit M17). Umsetzung: Spike bestanden, M17 gemergt.

→ Idee: [feature-ideen-magnifier.md](../../kb/feature-ideen-magnifier.md) (D1), Bewertung:
[feature-priorisierung.md](../../kb/feature-priorisierung.md)

## Entscheidungen (Grilling 2026-11-11)
| Thema | Entscheidung |
|---|---|
| Zeitpunkt Spike | parallel zu M17, harte Grenze |
| Bestanden, wenn | Text sichtbar besser lesbar · keine störenden Halos mit Invertieren / intelligentem Invertieren · ≤ **0,05 ms** Zusatz-GPU-Zeit auf RTX 3060 Ti bei 1080p |
| Onboard-Grafik | nicht vorhanden; ×10 hochgerechnet ≤ 0,5 ms, im Ergebnis als **hochgerechnet** markiert |
| Durchgefallen | D1 fällt aus M18; Ergebnis und Gründe bleiben dokumentiert |
| Bedienung | zwei stufenlose Regler: **Schärfe 0–100 %**, **Fett 0–3 px** |
| Werkswert | beide aus |
| Ort | Seite „Zoom & Modi“, gehört zum Profil |
| Bei 1× | wirkt nur, wenn gezoomt wird (ab 1,5×) *(selbst entschieden, im Spike prüfen)* |

## Umsetzung
- **Spike, zwei Wege vergleichen**
  1. **Im vorhandenen Durchgang:** in `Magnify.hlsl` `PSMain` zwischen `SampleDesktop(uv)` und
     `Apply(...)`. Das ist billig, aber Schärfen und Fett wirken dann vor der Farbmatrix und je
     Abtastpunkt. Es braucht mehr Abtastungen pro Pixel, Catmull-Rom hat schon 9.
  2. **Eigener Durchgang:** neues Offscreen-Ziel in Monitorgröße, `Magnify` schreibt dorthin.
     Ein neuer Shader `Enhance.hlsl` macht Unschärfemaske (Schärfe) und Minimum-Filter auf
     Leuchtdichte (Fett) und schreibt in die Swapchain, vor `HighlightLayer`. Das ändert
     `DesktopLayer.cs` (Ziel/Ressourcen beim Start/Resize anlegen, keine Allokation pro Frame).
- **Fett und Invertieren:** „Fett“ verbreitert dunkle Striche auf hellem Grund. Nach dem
  Invertieren ist Schrift hell, deshalb muss die Richtung sich am Ergebnis orientieren. Der Spike
  prüft das mit beiden Weg-Varianten.
- **Umsetzung M18**
  - Core: `TextEnhancement(Sharpness, BoldPx)` im `DisplayEffects`-Snapshot, `EffectsMapping`.
  - Rendering: der gewählte Weg.
  - App: zwei Regler mit Wert + Einheit.
  - D1 besitzt in M18 die Durchgangsstruktur von `DesktopLayer`.

## Aufgaben
### Spike (`spike/d1-schaerfen-fetter`, nie nach M17 gemergt)
- [ ] Beide Wege als Prototyp; Testbilder: 9-pt-Text hell/dunkel, invertiert, intelligent
      invertiert, Foto, Excel-Gitter
- [ ] GPU-Zeit je Weg mit Timestamp-Queries (vorhandene GPU-Messung), 1080p, Stufen 2×/4×/8×
- [ ] Debug-Schnappschüsse vorher/nachher; Nutzer bewertet die Lesbarkeit
- [ ] Ergebnis + Entscheidung in `docs/kb/rendering-pipeline.md` und hier (bestanden / durchgefallen,
      gewählter Weg, Messwerte „hochgerechnet“)
### Umsetzung M18 (nur wenn bestanden)
- [ ] Core: Modell, Mapping, Tests (Werte, Grenzen, Profil), Abdeckung ≥ 90 %
- [ ] Rendering (EventIds 3200–3219): Shader/Durchgang, WARP-Tests (Kante wird steiler, Strich wird
      breiter, invertiert ohne Halo), Benchmark Frame `0 B`
- [ ] Messwert `zoomin.render.enhance_gpu_ms` (Histogramm, beim Start angelegt); Log 3200 Durchgang
      angelegt/neu aufgebaut
- [ ] App (EventIds 1600–1619): zwei Regler auf „Zoom & Modi“, Texte DE/EN, Bildschirmleser liest
      Wert + Einheit; `ProfileFields`
- [ ] Katalog `telemetrie.md`, `performance.md` (neue Messwerte)

## Parallelisierung
- **Spike:** komplett eigene Spur, eigener Worktree, von `develop`. Er läuft parallel zu allen
  M17-Spuren, berührt keine M17-Dateien und wird nicht gemergt; nur das Ergebnis wandert in die Doku.
- **M18-Umsetzung, exklusiv:** `DesktopLayer.cs` (Durchgangsstruktur), `Enhance.hlsl` bzw. der
  Enhance-Teil in `Magnify.hlsl`.
- **Hotspots, nur ergänzend:** `Magnify.hlsl` (teilt sich M18 mit den Farbschemata; Reihenfolge
  im M18-Plan), `AppSettings.cs`, `ProfileFields.cs`, `EffectsMapping.cs`, `Texts*.cs`,
  `telemetrie.md`.
- **Merge in M18:** zuerst D1, weil es die Durchgangsstruktur festlegt; danach der Shader-Teil der
  Farbschemata.

## Akzeptanzkriterien
- [ ] Spike-Ergebnis dokumentiert mit Messwerten und Entscheidung
- [ ] (M18) Schärfe und Fett sichtbar wirksam, keine Halos mit Invertieren/intelligentem
      Invertieren (KI-Debug-Schnappschuss)
- [ ] (M18) Zusatz-GPU ≤ 0,05 ms auf RTX 3060 Ti bei 1080p, Benchmark `0 B`
- [ ] (M18) Ab Werk aus; bestehende Darstellung unverändert
- [ ] Overlay weiterhin aus Aufnahmen ausgeschlossen

## Offene Fragen / Risiken
- Eigener Durchgang kostet Bandbreite (Monitorgröße × 2). Auf Onboard-Grafik kann das mehr wiegen,
  als die Hochrechnung zeigt. Hardware-Berichte nach dem Release beobachten.
- Fett bei farbiger Schrift auf farbigem Grund ist unklar. Wahrscheinlich nur Leuchtdichte, im
  Spike zeigen.
- Intelligentes Invertieren nutzt Gewichtstexturen. Die Reihenfolge Enhance vor oder nach Smart
  Invert ist eine Spike-Frage.
