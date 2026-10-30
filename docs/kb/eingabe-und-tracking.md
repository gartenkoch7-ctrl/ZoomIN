# Eingabe & Tracking

## Problem: Klicks im vergrößerten Bild
Das Overlay ist durchklickbar – Klicks landen am **echten** Mauspunkt auf dem Desktop.
Damit das zum Bild passt, muss der echte Mauspunkt immer dort angezeigt werden, wo er
tatsächlich ist.

## MVP-Lösung: Cursor-Fixpunkt (ADR-0003)
Vergrößert wird um den Mauszeiger `p` als Fixpunkt:

```
angezeigt(x) = p + z · (x − p)      ⇒   angezeigt(p) = p
```
- Der Punkt unter der Maus stimmt immer → Klicks treffen ohne Sonderrechte.
- Bewegt sich die Maus an den Rand, wandert der Ausschnitt proportional mit; der gesamte
  Desktop bleibt erreichbar.
- Funktioniert ohne UIAccess, ohne Signatur, ohne Installation in `Program Files`.

## Tracking durch Fokus/Caret
Springt der Viewport zum Tastaturfokus/Caret, stimmen Bild und Mausposition nicht mehr
überein. MVP-Verhalten: Beim Fokus-Tracking wird die Maus **nicht** bewegt; sobald die Maus
bewegt wird, übernimmt wieder der Cursor-Fixpunkt. ⚠️ Mit Nutzern testen.

## Später: Input-Transform
`MagSetInputTransform` bildet Eingaben aufs vergrößerte Bild ab und erlaubt weitere
Tracking-Modi (Kanten-Push, zentriert). Voraussetzungen: `uiAccess="true"` im Manifest,
signierte EXE, Installation in einem geschützten Ordner. Erst mit Installer + Code-Signing.

## Tracking-Quellen
| Quelle | API | Hinweis |
|---|---|---|
| Maus | `GetCursorPos` pro Frame (+ Low-Level-Hook nur für Hotkeys/Gesten) | Polling im Render-Thread ist am latenzärmsten |
| Fokus | UIA `FocusChangedEvent` | Eigener STA/MTA-Thread, nie im Render-Thread |
| Caret | UIA `TextPattern` / `GetGUIThreadInfo` als Fallback | Viele Apps liefern kein UIA-Caret |

`Core.TrackingArbiter` entscheidet, welche Quelle gewinnt (zuletzt aktive, mit Prioritäten).

## Hotkeys (Vorschlag, konfigurierbar)
| Aktion | Standard |
|---|---|
| Zoom an/aus | `Win+Alt+Z` |
| Zoom + / − | `Win+Alt+Plus/Minus`, `Strg+Alt+Mausrad` |
| Modus wechseln | `Win+Alt+M` |
| Farbfilter an/aus | `Win+Alt+C` |
Kollisionen mit der Windows-Lupe (`Win+Plus`) vermeiden. Registrierung per `RegisterHotKey`.
