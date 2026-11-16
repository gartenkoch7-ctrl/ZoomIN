# D1 · Schärfen und Fetter
**Meilenstein:** Spike parallel zu [M17](../m17-ruhige-ansicht.md), Umsetzung in [M18](../m18-lesbarkeit.md) · **Spur:** M17-D (Spike), M18-A (Umsetzung) · **Branch:** `spike/d1-schaerfen-fetter` (von `develop`), danach `feat/m18-d1-schaerfen-fetter` · **Größe:** Spike S, Umsetzung M · **Status:** umgesetzt (Variante A), Sichtprüfung Nutzer offen

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
| Bei 1× | wirkt nur, wenn gezoomt wird (ab 1,5×) *(selbst entschieden, im Spike geprüft)* |
| Weg (nach Spike) | Variante A im vorhandenen Durchgang, eigener Einstieg `PSEnhanced` nur bei Schärfe oder Fett > 0 |

## Entscheidungen zu den Spike-Risiken (Umsetzung 2026-11-12)
Vorgaben des Nutzers, Ausgestaltung *selbst entschieden*. Details und Begründungen:
[rendering-pipeline.md › Schärfen und Fett](../../kb/rendering-pipeline.md#schärfen-und-fett-m18-d1).

| Risiko | Entscheidung |
|---|---|
| 1 ClearType-Säume durch Fett | Nur die Leuchtdichte des dunkelsten/hellsten Werts; der Pixel wird zu Schwarz/Weiß hin skaliert und behält seinen Farbton, die Sättigung sinkt nur *(selbst entschieden)*. Folge: verbreiterter Rand farbiger Schrift wird grau-dunkel |
| 2 Treppen an Fotokanten | Regel: 3–4 der 4 Eck-Abtastungen des Bikubik-Fensters farbig (Chroma 0,10–0,25 wie `Ambient.hlsl`) → Foto → **Schärfe und Fett blenden aus**. Ohne Zusatz-Abtastung, unabhängig vom intelligenten Invertieren *(selbst entschieden)*. Grenze: Text auf kräftig farbiger Fläche wird nicht verbessert, graue Fotos schon |
| 3 Glättung | Nur „Text-optimiert“; bei „Weich“/„Pixel“ sind beide Regler deaktiviert mit Hinweis „Wirkt nur mit der Glättung ‚Text-optimiert‘“, kein stilles Umschalten *(selbst entschieden)* |
| 4 Unter 1,5× | aus (`TextEnhancement.MinZoom`) *(selbst entschieden)* |

**Abweichungen vom Spike** *(selbst entschieden, Messung im WARP-Test)*:
- Schärfe ist nicht mehr der schärfere Keys-Kern: Der machte Kanten nicht steiler (Steigung
  `1,5 + a/2`), sondern nur Striche dunkler. Jetzt: Catmull-Rom an umgerechneter Position (Kante bei
  100 % ein Drittel Texel breit), gleiche 9 Abtastungen.
- Keine Begrenzung auf den Bereich der Abtastungen: Sie machte Kanten weicher (Mischwerte).
  Überschwingen bleibt wie bei Catmull-Rom heute, Schärfe fügt keines hinzu.
- Fett reicht höchstens ½ Texel weit (bei 2× also 1 px): Weiter entstanden Geisterkopien dünner
  Striche. Die Strich-Erkennung nutzt 9 Abtastungen und Kreuz zusammen.
- Kein App-Log (1600–1619 bleibt reserviert): Die Render-Seite protokolliert jede Änderung (3200/3201).

## Umsetzung
- **Core:** `TextEnhancement(Sharpness, BoldPixels)` in `DisplayEffects.Enhancement`; `At(Zoom,
  Glättung)` entscheidet je Frame (0 B). Einstellungen `TextSharpness`, `TextBold` (Profil,
  `ProfileFields`, `EffectsMapping`, sicherer Bildschirm begrenzt sie).
- **Rendering:** `Shaders/TextEnhance.hlsli`, `Magnify.hlsl` `PSEnhanced`; `MagnifyConstants` trägt
  Schärfe/Fett (128 B). `MagnifyPass` bindet `PSEnhanced` nur, wenn eines > 0 ist. `DesktopLayer`
  bleibt ein Durchgang. `SmartCostProbe` heißt jetzt `PassCostProbe` und misst auch D1
  (`EnhanceCost`). `EnhanceWatch` protokolliert.
- **App:** zwei `ValueSlider` auf „Zoom & Modi“ (Schärfe in %, Fett in px mit einer Nachkommastelle;
  Bildschirmleser hören Titel, Wert und Einheit, die Beschreibung als Hilfetext).

## Spike-Ergebnis (2026-11-11)
**Urteil: bestanden mit Variante A (im vorhandenen Durchgang)**. Variante B (eigener Durchgang)
ist **durchgefallen** (Budget, Nähte). Code: Branch `spike/d1-schaerfen-fetter` (nicht gemergt).

| Kriterium | A (Runde 2) | B (Post-Durchgang) |
|---|---|---|
| Zusatz-GPU 1080p, RTX 3060 Ti, 2×/4×/8×, ohne/mit Smart Invert | **0,026–0,029 ms** ✔ (≤ 0,05) | 0,146–0,155 ms ✘ |
| ×10 Onboard (**hochgerechnet**) | ≈ 0,29 ms ✔ (≤ 0,5) | ≈ 1,5 ms ✘ |
| Text sichtbar kräftiger/klarer (WARP-Bilder) | ja | ja |
| Halos invertiert / intelligent invertiert | keine | keine um Buchstaben, aber breitere Naht an Smart-Invert-Kanten |

- Enhance läuft **vor** Farbmatrix und Smart Invert (auf der Quelle); die Fett-Richtung folgt der
  Umgebung und ist dadurch für Invertieren egal.

## Ergebnis der Umsetzung (2026-11-12)
| Kriterium | Ergebnis |
|---|---|
| Zusatz-GPU 1080p, RTX 3060 Ti, 2×/4×/8×, 60 %/1 px und 100 %/3 px, ohne/mit Smart Invert (5 Läufe) | **0,018–0,030 ms** ✔ (≤ 0,05) |
| ×10 Onboard (**hochgerechnet**) | ≈ 0,3 ms ✔ (≤ 0,5) |
| Aus: Zusatzkosten | keine (`PSMain`, bitgleich, Test) |
| WARP-Tests | Kante steiler, Strich breiter (1 px und 3 px), kein Halo ohne Filter/invertiert/intelligent invertiert, ClearType nicht farbiger, Fotos unverändert, aus = wie vorher |
| Render-Loop | 0 B (`RenderLayersAllocationTests` mit Schärfe/Fett und laufender Kostenmessung, Benchmark `TextEnhancementAt`) |

## Aufgaben
### Spike (`spike/d1-schaerfen-fetter`, nie nach M17 gemergt)
- [x] Beide Wege als Prototyp; Testbilder: 9-pt-Text hell/dunkel, invertiert, intelligent
      invertiert, Foto, Excel-Gitter
- [x] GPU-Zeit je Weg mit Timestamp-Queries (vorhandene GPU-Messung), 1080p, Stufen 2×/4×/8×
- [ ] Debug-Schnappschüsse vorher/nachher; Nutzer bewertet die Lesbarkeit *(Vergleichsbilder per
      WARP erzeugt; Bewertung durch den Nutzer steht aus)*
- [x] Ergebnis + Entscheidung in `docs/kb/rendering-pipeline.md` und hier (bestanden / durchgefallen,
      gewählter Weg, Messwerte „hochgerechnet“)
### Umsetzung M18 (nur wenn bestanden)
- [x] Core: Modell, Mapping, Tests (Werte, Grenzen, Profil), Abdeckung ≥ 90 %
- [x] Rendering (EventIds 3200–3219): Shader/Durchgang, WARP-Tests (Kante wird steiler, Strich wird
      breiter, invertiert ohne Halo), Benchmark Frame `0 B`
- [x] Messwert `zoomin.render.enhance_gpu_ms` (Histogramm, beim Start angelegt); Log 3200
      an/aus/pausiert *(kein Durchgang wird angelegt – Variante A)*
- [x] App (EventIds 1600–1619): zwei Regler auf „Zoom & Modi“, Texte DE/EN, Bildschirmleser liest
      Wert + Einheit; `ProfileFields` *(kein App-Log nötig, Bereich reserviert)*
- [x] Katalog `telemetrie.md`, `performance.md` (neue Messwerte)

## Parallelisierung
- **Spike:** komplett eigene Spur, eigener Worktree, von `develop`. Er läuft parallel zu allen
  M17-Spuren, berührt keine M17-Dateien und wird nicht gemergt; nur das Ergebnis wandert in die Doku.
- **M18-Umsetzung, exklusiv:** `DesktopLayer.cs` (Durchgangsstruktur), `TextEnhance.hlsli` und der
  `PSEnhanced`-Teil in `Magnify.hlsl`.
- **Hotspots, nur ergänzend:** `Magnify.hlsl` (teilt sich M18 mit den Farbschemata; Reihenfolge
  im M18-Plan), `AppSettings.cs`, `ProfileFields.cs`, `EffectsMapping.cs`, `Texts*.cs`,
  `telemetrie.md`.
- **Merge in M18:** zuerst D1, weil es die Durchgangsstruktur festlegt; danach der Shader-Teil der
  Farbschemata. *Umgesetzt auf `feat/m18-c-farbschemata` (09e9eb0) als Basis, weil die Farbschemata
  `Magnify.hlsl` schon geändert hatten – beim Mergen also C vor D1 oder D1 enthält C bereits.*

## Akzeptanzkriterien
- [x] Spike-Ergebnis dokumentiert mit Messwerten und Entscheidung
- [ ] (M18) Schärfe und Fett sichtbar wirksam, keine Halos mit Invertieren/intelligentem
      Invertieren (KI-Debug-Schnappschuss) *(WARP-Tests grün; Schnappschuss und Sichtprüfung offen)*
- [x] (M18) Zusatz-GPU ≤ 0,05 ms auf RTX 3060 Ti bei 1080p, Benchmark `0 B`
- [x] (M18) Ab Werk aus; bestehende Darstellung unverändert
- [x] Overlay weiterhin aus Aufnahmen ausgeschlossen *(kein neues Fenster)*

## Offene Fragen / Risiken
- Sichtprüfung durch den Nutzer: Schärfe 100 % zeigt Texel deutlicher (auch ClearType-Farbpixel
  als kleine Flecken), Fett 3 px füllt bei 4× kleine Punzen. Ggf. Regelbereich oder Kurve anpassen.
- Text auf kräftig farbiger Fläche gilt als Foto und wird nicht verbessert.
- Ohne echte Onboard-Grafik bleiben die GPU-Werte hochgerechnet; `zoomin.render.enhance_gpu_ms`
  liefert sie aus Diagnosepaketen.
