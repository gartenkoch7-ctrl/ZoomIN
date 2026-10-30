# Testumgebung

Stand 2026-10-30. Grundlage für alle Mess- und Kompatibilitätsaussagen.

## Vorhandene Hardware
| Komponente | Wert |
|---|---|
| GPU | NVIDIA GeForce RTX 3060 Ti (Desktop), Treiber 32.0.16.1088 |
| Monitor | 1× AOC 27G2G4, 1920×1080, aktuell 60 Hz (laut Modell bis 144 Hz ⚠️ prüfen) |
| Betriebssystem | Windows 11 Home, Build 22631 |
| Weitere Geräte | keine |

## Strategie
| Bereich | Wie getestet |
|---|---|
| Bildrate / Latenz | Messung bei **60 Hz und 144 Hz** am echten Monitor |
| Multi-Monitor, gemischte DPI | **Virtuelle Monitore** (Indirect Display Driver) – Eignung in M3 prüfen |
| Shader / Rendering | **WARP** (Software-D3D) offscreen, lokal und in CI |
| Windows 10 | VirtualBox-VM mit Windows 10 2004+ (nur Grundfunktion; Treiber in VMs eingeschränkt) |
| Bildschirmfreigabe | Teams, Zoom/Webex, Discord, Browser-Freigabe (Edge/Chrome) |
| Hybrid-Grafik, Laptop-DPI, echte Zweitmonitore, HDR, ARM64 | **nicht testbar** → in Release-Notes als „ungetestet“ |

Keine Beta-Phase (ADR-0009): Rückmeldungen zu ungetesteter Hardware kommen über die
GitHub-Issue-Vorlage „Hardware-Bericht“.

## Ergebnisse
| Datum | Thema | Ergebnis |
|---|---|---|
| – | Desktop Duplication auf virtuellem Monitor | offen (M3) |
