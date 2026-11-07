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

## Modi und Klicks (M4)
| Modus | Geometrie | Klicks |
|---|---|---|
| Vollbild | ganzer Monitor, Fixpunkt Maus | treffen |
| Lupe | Rechteck um die Maus (im Monitor gehalten), Quelle = p + (Ziel − p) / z | treffen (Fixpunkt) |
| Angedockt | reservierter Streifen zeigt Umgebung der Maus aus dem Restbereich | im Streifen: reine Anzeige |

Rechnung in `Core/Modes/ModeGeometry.cs`, Beweis per Test (`Lens_CursorPixel_IsDisplayedAtCursor`).

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
| Fokus | UIA `GetFocusedElement` → `CurrentBoundingRectangle`, ~30×/s gepollt | Eigener MTA-Thread, nie im Render-Thread |
| Caret | `GetGUIThreadInfo` (System-Caret) ~30×/s | Apps ohne System-Caret liefern nichts |

`Core/Tracking/AnchorController` entscheidet pro Monitor, welche Quelle gewinnt (siehe M6-Plan).

## Kompatibilität Fokus / Caret (M6)
| Anwendung | Fokus | Caret | Quelle |
|---|---|---|---|
| WinForms/Win32-Textfelder | ✅ | ✅ | automatische Probe 2026-10-30 |
| Notepad, Explorer, Einstellungen | ? | ? | offen – Sichttest |
| Edge, Chrome, Firefox | ? | ? | offen |
| Word, Outlook, Excel | ? | ? | offen |
| VS Code, Teams, Discord | ? | ? | offen |
| Windows Terminal | ? | ? | offen |

## Zeiger-Auslöser (UI-Neugestaltung)
`Platform/Input/Win32InputActivityHook` beobachtet mit eigenem `WH_MOUSE_LL` + `WH_KEYBOARD_LL`
(nur lesen, nichts verschlucken) Mausbewegung und Tippen. `App/Pointer/PointerActivity` macht
daraus die Suchhilfe bei Schütteln (`Core/Pointer/ShakeDetector`: 4 Richtungswechsel à ≥ 40 px
in 0,8 s, Zittern < 8 px zählt nicht), nach ≥ 3 s Ruhe (`IdleDetector`) oder bei Strg allein
(gedrückt und losgelassen ohne andere Taste/Klick; AltGr sendet Strg+Alt und zählt nicht).
„Zeiger beim Tippen ausblenden“: Zeichentaste ohne Strg/Alt/Win, endet mit der nächsten
Mausbewegung. Pro Mausbewegung keine Allokation. Ohne Zoom und Filter zeichnet das Overlay
nur Zeiger/Fadenkreuz/Ring auf transparentem Grund, ohne den Desktop zu kopieren.

## Windows-Zeigereinstellungen überschreiben
Windows reicht über `GetCursorInfo` bereits vergrößerte bzw. eingefärbte Zeiger heraus
(Barrierefreiheit → Mauszeiger: `HKCU\Control Panel\Cursors\CursorBaseSize`, z. B. 112 statt 32;
die erzeugten Dateien liegen in `%LocalAppData%\Microsoft\Windows\Cursors\*_eoa.cur`). Mit
„Überschreiben“ lädt `Platform/Cursor/StandardPointers` stattdessen die unveränderten Originale
aus `%SystemRoot%\Cursors` per `LoadImage(LR_LOADFROMFILE)` in genau der benötigten Pixelgröße
(scharf statt hochskaliert; die aero-Dateien enthalten Bilder bis 128 px).
- Zuordnung Handle → Datei: System-Zeiger sind geteilte Handles, `GetCursorInfo` liefert dasselbe
  Handle wie `LoadCursor(NULL, IDC_*)`. I-Balken und Fadenkreuz haben keine aero-Datei, genutzt
  werden `beam_r.cur`/`cross_r.cur` (monochrom, Invertier-Pixel werden schwarz gezeichnet).
- Anwendungszeiger ohne Standard-Gegenstück: Windows-Bild, nur auf Standardgröße
  (32 px × System-DPI/96) verkleinert; ihre Windows-Farbe bleibt.
- `CursorShape.BaseSize`/`Density`: Bildpixel pro Bildschirmpixel bei Größe 1×. Geladen wird je
  Zeiger in Stufen 1/2/4/8× (`PointerStyle.SharpScale`), damit die Zoom-Animation höchstens vier
  Bilder braucht und keines mehr als halbiert wird (bilinear ohne Mipmaps bleibt glatt).
- Größe: `UnzoomedScale × Zoom`, gedeckelt bei `MaxScale` (nie unter der Größe ohne Zoom);
  „Größe relativ zum Zoom“ aus = immer `UnzoomedScale`. Mit Überschreiben zeichnet ZoomIN den
  Zeiger auch ohne Zoom (Overlay läuft dauerhaft).
- ⚠️ Mehrere Monitore mit unterschiedlicher DPI: Grundgröße folgt der System-DPI, nicht der des
  Monitors – im Sichttest prüfen.

## Tastenkürzel
Schema und Standardbelegung: [ADR-0008](../adr/0008-tastenkuerzel-strg-alt.md)
(Strg+Alt+Taste ohne AltGr-Belegung, Strg+Mausrad mit Vorrang vor Anwendungen).
Globale Kürzel per `RegisterHotKey`, Mausrad per `WH_MOUSE_LL`.

## Fokus & Caret – Verhalten (M6)
- Fokus-Schwenk weich (~200 ms) oder sofort, einstellbar; nur bewegen, wenn das Element
  nicht schon sichtbar ist.
- Caret mit Randabstand (Standard 20 %).
- Mausbewegung über Schwellwert → gleitend zurück zum Maus-Fixpunkt.

## Windows-Benachrichtigungen (Toasts)
Erscheint eine Windows-Benachrichtigung (z. B. Teams mit Stil „Windows-Benachrichtigungen“),
schwenkt die Ansicht hin und zeigt sie in Originalfarben.

**Entscheidungen (Nutzer, 2026-11-03):**
- Alle Toasts, nicht nur Teams (keine Absender-Unterscheidung).
- Automatisch hinschwenken, zusätzlich **Strg+Alt+Umschalt+N** (`GoToNotification`) – springt
  auch, wenn „Zu Benachrichtigungen schwenken“ aus ist oder der Toast schon im Bild ist.
- Bleibt dort, solange der Toast sichtbar ist; Mausbewegung (> 8 px) holt die Ansicht sofort
  zurück, Schließen des Toasts ebenfalls. Bewegt sich die Maus gerade, wenn der Toast erscheint,
  wird der Schwenk nachgeholt, sobald sie 0,5 s ruht (Toast noch sichtbar).
- Farbfilter gilt im Toast-Rechteck nicht, der Rest bleibt gefiltert – unabhängig davon, ob die
  Ansicht gerade dort steht. Abschaltbar („Benachrichtigungen in Originalfarben“).
- Unabhängig von Strg+Alt+T (Fokus/Caret aus ≠ Toasts aus); eigene Schalter je Profil.
- Zentriert wird vorerst auf die Rechteckmitte; nach dem Merge von `fix/focus-centering` auf
  `ViewCentering.AnchorFor` umstellen.

**Erkennung** (`Platform/Automation/ToastWindowFinder`, im 33-ms-Takt von `UiaTrackingSource`):
Top-Level-Fenster der Klasse `Windows.UI.Core.CoreWindow` aus `ShellExperienceHost.exe`, sichtbar
und nicht per DWM verhüllt (`DWMWA_CLOAKED` = 0). Derselbe Prozess hält Benachrichtigungscenter
und Kalender-Flyout; die trennt `Core/Tracking/NotificationRules` über die Größe
(Breite ≤ 40 %, Höhe ≤ 60 % des Monitors, Mitte auf dem Monitor). Erster Fund je Fenster wird
geloggt („Notification window found …“).

**Rendering:** Allgemein als *Farbbereich* angelegt (`Core/Filters/ColorRegion`): ein Rechteck
in Desktop-Koordinaten mit eigener Farbmatrix B, die dort die Monitor-Matrix A ersetzt. Für
Toasts ist B die Einheitsmatrix (Originalfarben). Genau ein Bereich pro Frame;
`ColorRegion.For` entscheidet, welche Quelle ihn füllt – vorgesehen für eine zweite Quelle
(geplanter „Freigabe-Filter“ nur für den beim Teams-Teilen freigegebenen Bereich).
`Magnify.hlsl`: `RegionMin/RegionMax` (Desktop-UV) und `RegionRow0..2`, Konstantenpuffer 176 B;
leeres Rechteck (0,0,0,0) = kein Bereich. Hot Path: 0 B (`AnchorWithNotification` ≈ 8 ns/Frame).

**Probe auf dem Entwickler-PC (Win 11 23H2, Build 22631, 2026-11-03):** Test-Toasts per
`ToastNotificationManager` (AppID PowerShell) landeten im Verlauf (`History.GetHistory` = 4),
ein Banner erschien aber nicht – auch nicht mit ausgeblendetem Overlay; Teams-Toast kam ebenfalls
nicht. `SHQueryUserNotificationState` = 5 (`QUNS_ACCEPTS_NOTIFICATIONS`). Banner werden auf diesem
PC also unterdrückt (vermutlich „Nicht stören“ bzw. Banner-Einstellung). Folge:
⚠️ **Die Fenstermerkmale oben sind noch nicht am echten Banner gemessen**, sondern aus dem
bekannten Aufbau der Windows-Shell übernommen. Offener Test: Banner zulassen, Toast auslösen,
Log-Zeile „Notification window found“ prüfen; falls sie fehlt, mit
`EnumWindows` (Klasse, Prozess, Cloaked, Rechteck) nachmessen und hier eintragen.
Zu prüfen ist dabei auch, ob Lautstärke-/Helligkeitsanzeigen fälschlich als Toast gelten.
