# SuperNova – Black-Box-Analyse

> **Clean-Room:** Nur Beobachtungen aus Benutzung und öffentlichen Quellen, in eigenen
> Worten. Kein Dekompilieren, keine Screenshots, keine kopierten Texte/Assets.
> Jede Zeile mit Quelle. Siehe AGENTS.md, Abschnitt 3.

## Produkt
- Hersteller: Dolphin Computer Access Ltd. (UK)
- Varianten: Magnifier · Magnifier & Speech · Magnifier & Screen Reader (inkl. Braille)
- Quelle: öffentliche Produktseite des Herstellers. ⚠️ Details bei Bedarf verifizieren.

## Funktionen (öffentlich beschrieben) → Abbildung in ZoomIN
| SuperNova-Funktion | Beobachtung / Beschreibung | ZoomIN | Status |
|---|---|---|---|
| Vollbild-Vergrößerung | Ganzer Bildschirm, 1,2× bis 64× in 28 Stufen (4,5× kam mit 25.02 dazu) ¹ ⁷ | Vollbild-Modus | MVP |
| Lupe (Lens) | Vergrößerungsfenster folgt Zeiger und Fokus ¹ | Lens-Modus | MVP |
| Angedockt / Split | Teilbildschirm oben/unten/links/rechts, Teilung verstellbar ¹ | Docked-Modus | MVP |
| Festes Fenster | Vergrößerung in einem frei platzierten, stehenden Fenster ¹ | – | Idee B3 |
| Automatische Lupe | Lupe passt ihre Größe dem fokussierten Element an ¹ | – | Backlog (Idee B5) |
| Übersicht | Kürzel zeigt 1×, der vergrößerte Bereich ist darin farblich abgesetzt ¹ | – | Idee B2 |
| Feste Bereiche | Aufgezogene Bildschirmbereiche bleiben mit eigener Stufe vergrößert sichtbar, gemeinsam ein-/ausblendbar; nicht an Steuerelemente gebunden ² | – | Idee B1 |
| Line View | Text einer Zeile laufend, Schrift/Farbe/Tempo wählbar ³ | – | Backlog |
| Farbschemata | über 20 Vorgaben, auch für Farbsehschwächen; Helligkeit, Kontrast, Tönung, Farbe ersetzen ⁴ ⁵ | Farbfilter | MVP (Rest: Ideen C1–C3) |
| Zeiger-/Fokushervorhebung | Zeigergröße fest oder relativ, sehr viele Zeigerbilder; Hervorhebung dauerhaft, per Kürzel oder je nach Maus/Tastatur ⁴ ⁵ | Hervorhebung | MVP |
| Font-Glättung bei hoher Vergrößerung | Stufen: keine / normal / Neuzeichnen der Schrift in unterstützten Programmen, sonst normal; zusätzlich „fetter“ (3 Stufen) und „schärfen“ ¹ | Shader-Glättung | MVP (einfach), Idee D1 |
| Schwenken per Tastatur | Taste halten + Pfeile; gleiche Richtung beschleunigt, Gegenrichtung bremst; Sprung an Ränder/Mitte, Positionen merken ¹ ⁵ | – | Idee A3 |
| Achsensperre | Umschalt halten → Bewegung nur waagrecht oder senkrecht ¹ | – | Idee A6 |
| Randfreigabe | Zusatzfläche um den Rand, damit der Zeiger mittig bleiben kann ³ | – | Idee A2 |
| Mehrere Monitore | Strecken, Nebeneinander, Klonen, Präsentationsmodus, Maus-Puffer am Rand, Monitorwechsel per Kürzel, Stufe und Farben je Monitor ⁶ | Zoom pro Monitor | Ideen A6, C4, F1, F2 |
| Kamera / Tafel | Webcam oder Lehrer-PC (lokales Netz, seit 25.01 auch Internet) vergrößert, gedreht, eingefroren ³ ⁸ | – | verworfen (F5/F8) |
| Verpasste Meldungen | Benachrichtigungen nachträglich durchgehen (25.01) ⁸ | – | Idee A4 |
| Sprachausgabe | Vorlesen unter Maus/Fokus | `ZoomIN.Speech` | Später |
| Screenreader / Braille | Volle Bildschirmlesefunktion | – | Nicht geplant |

## Offene Beobachtungsfragen
Mit installierter Testversion beobachten und hier (in eigenen Worten) festhalten:
- [ ] Wie fühlt sich das Zoom-Tempo beim Mausrad an (Stufen vs. stufenlos)?
- [ ] Wie verhält sich der Viewport am Bildschirmrand?
- [ ] Wie wird zwischen Maus- und Fokus-Tracking umgeschaltet?
- [ ] Ist SuperNova in Teams-Übertragungen sichtbar? (Unser Unterscheidungsmerkmal)
- [ ] Gemessene Latenz/Bildrate auf derselben Hardware wie ZoomIN

## Beobachtungsprotokoll
| Datum | SuperNova-Version | Quelle | Beobachtung (eigene Worte) |
|---|---|---|---|
| 2026-11-11 | 25.01 / 25.02 | Release-Seiten ⁷ ⁸ | 25.01 (23.02.2026): verpasste Meldungen nachlesen, Tafel übers Internet; Schärfe-Korrektur für Windows 11 25H2 mit Intel-Grafik. 25.02 (23.04.2026): Stufe 4,5×, Startmenü schneller vergrößert, gedrehter/skalierter Text besser |
| 2026-11-11 | 24.x | Hersteller-KB ⁹ | Nach Windows-Grafik-Updates (Nov. 2025) Bildreste/Spuren beim Vergrößern; Abhilfe per Hotfix und Option „alternatives Vergrößerungssystem“ – die Engine hängt also stark an Windows-Interna |
| 2026-11-11 | – | Bereiche-Lektion ² | Feste Bereiche werden in einem eigenen Bearbeitungsmodus mit blinkendem Rahmen per Maus und Tasten gesetzt, Stufe je Bereich |

Quellen (alle 2026-11-11 abgerufen, Recherche für [feature-ideen-magnifier.md](feature-ideen-magnifier.md)):
1. https://learn.yourdolphin.com/supernova-magnifier?lesson=magnification&hsLang=en
2. https://learn.yourdolphin.com/supernova-magnifier?lesson=hooked-areas&hsLang=en
3. https://yourdolphin.com/product/features?id=4&pid=4
4. https://learn.yourdolphin.com/supernova-magnifier?lesson=screen-enhancements&hsLang=en
5. https://yourdolphin.com/product/features?id=1&pid=1
6. https://learn.yourdolphin.com/supernova-magnifier?lesson=multiple-monitors&hsLang=en
7. https://yourdolphin.com/news?id=564
8. https://yourdolphin.com/product/version?id=646
9. https://kb.yourdolphin.com/knowledge/screen-artifacts-in-supernova-24
