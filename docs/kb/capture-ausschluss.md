# Unsichtbar bei Bildschirmübertragung

## Anforderung
Teilt der Nutzer seinen Bildschirm (Teams, Zoom, Webex, OBS, Snipping Tool), sehen die
anderen den **normalen, unvergrößerten** Desktop. Nur auf dem physischen Monitor ist der
Zoom sichtbar.

## Lösung
- Wir zeichnen den Zoom in ein eigenes **Overlay-Fenster** (topmost, randlos,
  `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`).
- Darauf: `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` (ab Windows 10 2004).
- Aufnahme-Software, die über Desktop Duplication oder Windows.Graphics.Capture aufnimmt,
  sieht das Fenster nicht → sie sieht den Desktop darunter.
- **Bonus:** Unsere eigene Desktop Duplication sieht das Overlay ebenfalls nicht → keine
  Rückkopplung („Spiegel im Spiegel“).

## Warum nicht die Windows-Magnification-API?
`MagSetFullscreenTransform` verändert die Ausgabe des Desktop Window Managers selbst; das
Ergebnis kann **nicht** aus Aufnahmen ausgeschlossen werden. Deshalb ist sie für die Anzeige
verboten (ADR-0002). `MagSetInputTransform` (nur Eingabe) bleibt für später erlaubt.

## Grenzen
| Fall | Verhalten |
|---|---|
| Teams/Zoom/OBS/Snipping Tool | Zoom unsichtbar ✅ |
| Hardware-Capture (HDMI-Splitter, Capture-Karte, Beamer im Spiegelmodus) | Zoom **sichtbar** – physikalisch nicht vermeidbar |
| Windows < 10 2004 | `WDA_EXCLUDEFROMCAPTURE` fehlt → nicht unterstützt |
| Alte Aufnahme-Software mit GDI-`BitBlt` | ⚠️ unverifiziert – testen und hier dokumentieren |

## Kontextmenüs und Popups: Overlay wieder nach oben holen
Gemeldet 2026-11-02: Rechtsklick-Menüs, aufgeklappte Chrome-Lesezeichenordner und ähnliche Popups
erscheinen **doppelt** – vergrößert im Zoom und unvergrößert darüber. Ursache: Diese Popups sind
selbst Topmost-Fenster und werden **nach** dem Overlay erzeugt; unter Topmost-Fenstern gewinnt das
zuletzt nach oben gebrachte. Anders als Shell-Menüs liegen sie aber in derselben Ebene wie wir.

Lösung (ADR-0013): `OverlayZOrderGuard` hört per `SetWinEventHook` (out-of-context, eigener
Prozess ausgeschlossen) auf `EVENT_SYSTEM_FOREGROUND`, `EVENT_SYSTEM_MENUPOPUPSTART` und
`EVENT_OBJECT_SHOW`. Zeigt ein fremdes **Top-Level-Fenster** sich selbst (`OBJID_WINDOW`), setzt
das Overlay sich per `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE …)` wieder an die Spitze.
Entscheidung als testbare Regel: `ZoomIN.Core.Overlay.OverlayRaisePolicy`.
- Hooks laufen auf dem Overlay-Thread (dessen Message-Loop liefert die Events), nicht im Render-Loop.
- Das Popup ist höchstens einen Augenblick unvergrößert sichtbar, bis das Event ankommt.
- Klicks gehen weiter durch das Overlay (`WS_EX_TRANSPARENT`, `HTTRANSPARENT`) ins Menü.
- Geprüft 2026-11-02 (Build 22631) per Z-Order-Auflistung (`GetTopWindow`/`GetWindow`):
  Ohne Guard liegt ein Fenstermenü (`#32768`) direkt **über** `ZoomIN.Overlay`, mit Guard direkt
  darunter. Rechtsklick-Menüs auf dem Arbeitsrechner des Nutzers ebenfalls ✅.
- Das Ton-/Netzwerk-Menü (Schnelleinstellungen) bleibt mit `dotnet run` und ZIP sichtbar – erwartet,
  siehe nächster Abschnitt. ⚠️ Chrome-Lesezeichenordner und Tooltips noch einzeln bestätigen.
- Test-Falle: ZoomIN läuft nur einmal (`SingleInstance`). Läuft noch eine ältere Instanz (Tray,
  Autostart), beendet sich der neu gestartete Build sofort, und man testet den alten Stand.

## Z-Ebenen: Shell-Menüs liegen über jedem Topmost-Fenster
Windows ordnet Fenster nicht nur nach „topmost“, sondern zusätzlich in **Ebenen (Bands)**. Die
Schnelleinstellungen (Ton, Netzwerk), das Info-Center und das Startmenü liegen in einer Ebene über
allen normalen Topmost-Fenstern. Folge ohne UIAccess (gemeldet 2026-10-30, Windows 11):
- Desktop Duplication nimmt das Menü auf → es erscheint **vergrößert und gefiltert** im Zoom.
- Das echte Menü liegt aber **über** dem Overlay → es bleibt zusätzlich **unvergrößert und
  ungefiltert** an seiner Stelle stehen.

Kein Fensterstil hilft (`WS_EX_TOPMOST`, `SetWindowPos(HWND_TOPMOST)`); `CreateWindowInBand` ist
für normale Prozesse gesperrt. Lösung: der **UIAccess-Build** (ADR-0012) – Topmost-Fenster eines
UIAccess-Prozesses kommen in die UIAccess-Ebene über diesen Menüs. Voraussetzung: signierte EXE in
`Program Files`.

Gemessen 2026-11-02 (Build 22631, `GetWindowBand` aus user32, undokumentiert):
- Die Schnelleinstellungen (Ton, Netzwerk, Bluetooth) und das Startmenü hostet Explorer in
  `XamlExplorerHostIslandWindow`, **Band 16** (`ZBID_SYSTEM_TOOLS`).
- Normales Overlay (`dotnet run`, ZIP): **Band 1** (`ZBID_DESKTOP`) → unter dem Menü.
- UIAccess-Build aus `Program Files`: **Band 2** (`ZBID_UIACCESS`), ganz oben in der Z-Order,
  über allen Band-16-Fenstern. Die Bandnummer sagt nichts über die Reihenfolge aus.
- Lokal installieren ohne Inno Setup: `setup-signing.ps1` (Admin, einmalig) →
  `publish.ps1 -UiAccess` → `install-uiaccess.ps1` (Admin). Siehe build-und-tooling.md.
- Simulierte Klicks/`Win+A` (`mouse_event`/`keybd_event`) öffnen die Schnelleinstellungen nicht –
  der Sichttest muss von Hand erfolgen.
- Sichttest 2026-11-02 (Build 22631, UIAccess-Build aus `Program Files`): Ton/Netzwerk/Bluetooth-Menü
  nur noch vergrößert, kein Original darüber ✅.
- Erkennen zur Laufzeit: `GetTokenInformation(TokenUIAccess)` auf dem eigenen Token
  (`Win32UiAccessProbe`). Beobachtet 2026-11-03: Auf einem zweiten PC mit selbst gebautem develop-Stand
  fehlte UIAccess, das Original-Menü lag wieder über dem Zoom. Seitdem zeigt ZoomIN dann Tray-Meldung
  und Banner mit Setup-Knopf (ADR-0017). Nebenbei: Bei einem UIAccess-Prozess liefern
  `Get-Process`/`Win32_Process` als normaler Benutzer **keinen** Pfad – ein leerer Pfad ist ein
  Hinweis, dass UIAccess aktiv ist.
- Auch die **Taskleiste** liegt ohne UIAccess über dem Vollbild-Zoom (beobachtet 2026-11-03 mit
  Dev-Build: Zoom „komisch an der Seite der Taskleiste“; mit dem UIAccess-Build normal).

## Pflichttest (manuell, vor jedem Release)
1. Zoom aktivieren (alle drei Modi).
2. Bildschirm freigeben in: **Microsoft Teams**, **Zoom/Webex**, **Discord**,
   **Browser-Freigabe** (Edge/Chrome, z. B. Google Meet); zusätzlich `Win+Shift+S`.
3. Erwartung: Aufnahme zeigt unvergrößerten Desktop, Hervorhebungen ebenfalls unsichtbar.
Ergebnisse mit Windows-Build und App-Versionen hier eintragen.

## Testprotokoll
| Datum | Windows-Build | Tool | Ergebnis |
|---|---|---|---|
| 2026-10-30 | 22631 | GDI `CopyFromScreen` (BitBlt), automatisiert | ✅ unsichtbar – Bild mit 2×-Zoom = Bild ohne Zoom (0,14 % Abweichung, Rauschen 0,08 %); Overlay `affinity=0x11` |
| – | 22631 | Teams / Zoom / Discord / Browser-Freigabe | offen – manueller Test (SuperNova vorher beenden!) |

Hinweis: Beim Testen darf **keine andere Lupe** (SuperNova, Windows-Bildschirmlupe) aktiv sein,
sonst ist nicht unterscheidbar, wessen Zoom zu sehen ist.
