# Eigene Farbschemata (M18, C1–C3)

Bis zu vier eigene, benannte Schemata. Plan: [c-eigene-farbschemata.md](../plan/features/c-eigene-farbschemata.md).
Shader-Seite: [rendering-pipeline.md](rendering-pipeline.md#eigene-farbschemata-m18-c1c3).

## Datenmodell
- `FilterId` (früher Enum `ColorFilter`): die 8 eingebauten Filter **unverändert in Name und Zahl**,
  danach `Custom1`–`Custom4` als feste Plätze. Gespeichert wird der Name (`UseStringEnumConverter`),
  daher liest jede alte `settings.json` ohne Migration und ohne Verlust (Test
  `SchemeSettingsMigrationTests`). Die Einstellung heißt weiter `ColorFilter`.
- `CustomScheme { Id, Name, Kind, Color1, Color2, Hue, Deficiency, Strength, Replacements[≤3] }`,
  `ColorReplacement { From, To, Tolerance }`; Farben als `"#RRGGBB"` wie beim Zeiger.
- Definitionen in `AppSettings.CustomSchemes` – **global**, nur in `settings.json`. Die Auswahl
  (`ColorFilter`, `MonitorFilters`) gehört zum Profil. Profildateien werden mit
  `ProfileFields.ForProfileFile` in `JsonProfileStore.Save` (gilt für jeden Weg, auch Import) ohne Schemata geschrieben (`"CustomSchemes": []`); `EffectsModel.Replace`
  (Profilwechsel) behält die laufenden Schemata. Ein exportiertes Profil nennt also nur Plätze,
  keine Definitionen; auf einem anderen PC zeigt es dessen Schema im Platz oder „Invertiert“.
- Ein Platz behält seine Nummer, wenn ein anderes Schema gelöscht wird. Leerer Platz oder unbekannter
  Wert → wirkt wie „Invertiert“ (`CustomSchemes.Resolve`).
- **Löschen** (`SchemeRemoval`): gemeinsamer Filter, Monitorfilter, Fensterregeln, Automatik-Filter
  und **gespeicherte Profile** fallen auf „Invertiert“ zurück; die Seite nennt die Anzahl.
- **Kein Rückweg zu älteren Versionen:** Ältere Builds kennen `Custom1`–`Custom4` nicht. Steht
  ein eigenes Schema in `ColorFilter`, `MonitorFilters`, einer Regel oder `AutoFilter`, scheitert dort
  das Lesen von `settings.json` (der String-Enum-Konverter wirft); die ältere Version sichert die Datei
  als `.broken` und startet mit Werkseinstellungen. Vor einem Downgrade eingebaute Filter wählen.
- **Gleichzeitiger Profilwechsel:** Wechselt ein Kürzel genau während des Löschens das Profil, kann
  der Platz im neuen Profil stehen bleiben (zeigt dann „Invertiert“). Hingenommen, sehr selten.
- **Grenze der festen Plätze:** Ein neues Schema bekommt den ersten freien Platz, also auch den eines
  gelöschten. Was den Platz noch nennt, ohne beim Löschen umgestellt worden zu sein (ein Profil, das
  nicht geschrieben werden konnte – Log 1625 –, eine vorher exportierte Profildatei), zeigt dann das
  neue Schema statt „Invertiert“. Bewusst hingenommen: vier Plätze, und Profile sind lokal.
- Bereinigung (`CustomSchemes.Normalized`) beim Laden, für den sicheren Desktop und nach jeder
  Änderung: höchstens 4, keine doppelten Plätze, Name ≤ 40 Zeichen, Stärke 0–100, ≤ 3 Ersetzungen.

## Matrizen (`SchemeMatrices`)
Alle Arten sind eine affine 3×3-Matrix wie die eingebauten Filter, also gleiche Shaderkosten.
L = 0,2126·R + 0,7152·G + 0,0722·B (Rec. 709, wie `ColorFilters`).

| Art | Formel | Weiß | Schwarz |
|---|---|---|---|
| Zwei Farben | `c2 + L·(c1 − c2)` | c1 | c2 |
| Einfärben | `L·Farbton` | Farbton | Schwarz |
| Farbsehschwäche | `I + s·S·(I − Sim)` | Weiß | Schwarz |
| Nur ersetzen | Einheitsmatrix | Weiß | Schwarz |

**Farbsehschwäche (nur Korrektur, keine Simulation):** Daltonisierung nach
Fidaner, Lin, Ozguven (2005), „Analysis of Color Blindness“ (Stanford, Projektbericht):
1. Was die Person sieht: `Sim` = Simulation einer vollständigen Dichromasie (Schweregrad 1,0) nach
   Machado, Oliveira, Fernandes (2009), „A Physiologically-based Model for Simulation of Color Vision
   Deficiency“, IEEE TVCG 15(6), 1291–1298 – die dort veröffentlichten RGB-Matrizen für Protan, Deutan,
   Tritan. Jede Zeile summiert zu 1, deshalb bleiben Grau, Weiß und Schwarz unverändert.
2. Was verloren geht: `E = (I − Sim)·c`.
3. Wohin es verschoben wird (`S`): bei Rot-Grün (Protan/Deutan) nach Fidaner `G += 0,7·E_R + E_G`,
   `B += 0,7·E_R + E_B`, Rot bleibt. Bei Tritan entsprechend gespiegelt (eigene Herleitung): Blau-Fehler
   nach Rot und Grün, `R += E_R + 0,7·E_B`, `G += E_G + 0,7·E_B`, Blau bleibt.
4. Stärke s (0–1) mischt mit der Einheitsmatrix: `I + s·(M − I)`; 0 % = Einheitsmatrix (Test).

Beispiel Deutan, 100 %: Rot (1, 0, 0) → (1, 0,16, 0,46) – Rot bekommt Blau und unterscheidet sich so
von Grün. **Näherung:** Machado rechnet in linearem RGB, ZoomIN wendet die Matrix wie alle Filter auf
die sRGB-codierten Werte an (eine Matrix, keine Umrechnung im Shader). Mit Betroffenen testen.

## Farbe ersetzen (`ReplacementTable`, `ColorReplace.hlsli`)
- Keine Matrix, eigener Parameterblock im Constant Buffer; Schritt vor der Matrix und vor der
  intelligenten Gewichtung („erst ersetzen, dann gewichten“). Folge (Plan-Entscheidung): Bei „Zwei
  Farben“, „Einfärben“ und „Farbsehschwäche“ läuft die neue Farbe noch durch die Matrix; genau so
  sichtbar wie gewählt ist sie nur bei „Nur Farben ersetzen“ (ohne Kontrast/Helligkeit). Die
  Vorschau zeigt das Ergebnis, die Seite sagt „Ersetzt wird vor dem Filter“.
- Abstand d im YCbCr-Raum (Rec. 709: Y, Cb = (B − Y)/1,8556, Cr = (R − Y)/1,5748), billig und
  ohne Gamma-Umrechnung; Lab wäre genauer, kostet aber Potenzen je Pixel.
- Radius `r = 0,01 + 0,5·Toleranz` (0–1). Gewicht `w = 1 − smoothstep(r/2, r, d)`. Toleranz 0
  trifft nur die Farbe selbst (Nachbarfarbe 16 Stufen daneben bleibt, Test).
- Verschiebung statt Ersetzen: `c + (neu − alt)·w`. Die Farbe selbst wird exakt die neue, ihre
  geglätteten Kanten und Schattierungen wandern mit. Mehrere Ersetzungen addieren sich, Reihenfolge egal.
- Kosten: siehe rendering-pipeline.md (+0,008 ms bei 1080p auf RTX 3060 Ti mit 3 Ersetzungen).

## Oberfläche
- Farbfilter-Seite → Karte „Eigene Farbschemata“ → Detailseite `CustomSchemesPage`: Liste mit
  „Bearbeiten“, „Neues Schema“ (ein fünftes wird mit Hinweis abgelehnt), Editor (Name, Art, Farben,
  Stärke, Ersetzungen), Vorschau (7 Farbpaare vorher/nachher, je ein Tab-Stopp mit Ansage
  „Rot wird zu #…“), „Jetzt anzeigen“, „Schema löschen“ mit Rückfrage.
- Änderungen wirken sofort (`CustomSchemeService.Update` → `EffectsModel.UpdateSchemes`).
- Ergebnis von Anlegen/Löschen in einer Live-Region (Bildschirmleser liest es vor).
