# ADR-0008: Tastenkürzel-Schema Strg+Alt (AltGr-sicher)

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Kontext
Wunsch: Kürzel beginnen mit Strg. Strg+einzelne Taste würde systemweit Grundfunktionen
blockieren (Strg+C, Strg+Z …). Auf deutschen Tastaturen ist AltGr = Strg+Alt, daher würden
Kürzel wie Strg+Alt+Q das Zeichen @ verhindern.

## Entscheidung
- Globale Kürzel: **Strg+Alt+Taste**, nur Tasten **ohne** AltGr-Belegung
  (verboten: Q, E, M, 2, 3, 7, 8, 9, 0, ß, +, <).
- Zoomen per Mausrad: **Strg+Mausrad**. ZoomIN fängt das Ereignis ab und hat Vorrang vor dem
  Zoom der jeweiligen Anwendung (Browser, Office). Hinweis im Erststart-Assistenten.
- Alles ist anpassbar; der Hotkey-Editor (M7) warnt vor AltGr- und Systemkollisionen.

## Standardbelegung
| Aktion | Kürzel |
|---|---|
| Zoom an/aus | Strg+Alt+Z |
| Vollbild / Lupe / Angedockt | Strg+Alt+F / L / D |
| Zoom + / − | Strg+Mausrad · Strg+Alt+Bild↑ / Bild↓ |
| Farbfilter an/aus | Strg+Alt+C |
| Zeiger-Suchhilfe | Strg+Alt+S |
| Profil wechseln | Strg+Alt+P |
| Lupengröße | Strg+Alt+Umschalt+Pfeiltasten |

Strg+Alt+Pfeiltasten werden gemieden: manche Intel-Grafiktreiber drehen damit den Bildschirm.

## Konsequenzen
- Strg+Mausrad braucht einen Low-Level-Maus-Hook (`WH_MOUSE_LL`), der das Ereignis
  verschluckt. Hook-Callback muss extrem schnell sein (M2).
