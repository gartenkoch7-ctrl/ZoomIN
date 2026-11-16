# ADR-0013: Overlay bei neuen Popups wieder nach oben holen

- **Status:** Angenommen (ergänzt ADR-0012)
- **Datum:** 2026-11-02

## Kontext
Fehlerbericht: Kontextmenüs (Rechtsklick), aufgeklappte Chrome-Lesezeichenordner und ähnliche
Popups erscheinen doppelt – vergrößert im Zoom und unvergrößert darüber. Diese Popups sind
Topmost-Fenster derselben Z-Ebene wie unser Overlay, werden aber später gezeigt und landen damit
über ihm. Der UIAccess-Build (ADR-0012) löst das zwar mit, ist aber nur im signierten Installer
aktiv; Entwickler-Builds und das portable ZIP brauchen eine eigene Lösung.

## Entscheidung
- Jedes Overlay installiert auf seinem eigenen Thread `SetWinEventHook`-Hooks
  (`WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`) für `EVENT_SYSTEM_FOREGROUND`,
  `EVENT_SYSTEM_MENUPOPUPSTART` und `EVENT_OBJECT_SHOW`.
- Zeigt sich ein fremdes Top-Level-Fenster, ruft das Overlay
  `SetWindowPos(HWND_TOPMOST, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)` auf.
- Die Entscheidung („anheben ja/nein“) liegt testbar in `ZoomIN.Core.Overlay.OverlayRaisePolicy`.

## Verworfen
- **Timer, der regelmäßig anhebt:** kostet dauerhaft CPU und reagiert trotzdem verzögert.
- **Popups ausblenden oder verschieben:** Eingriff in fremde Fenster, bricht deren Verhalten.

## Konsequenzen
- Neue Popups sind bis zum Eintreffen des Events (wenige Millisekunden) unvergrößert sichtbar.
- Keine Ping-Pong-Gefahr: Wir reagieren nur auf *Zeigen*, nicht auf Z-Order-Änderungen; eigene
  Fenster (andere Monitore, Einstellungen) sind ausgeschlossen.
- Shell-Flyouts (Schnelleinstellungen, Info-Center, Startmenü) liegen in einer höheren Ebene und
  bleiben weiterhin nur mit dem UIAccess-Build abgedeckt.
