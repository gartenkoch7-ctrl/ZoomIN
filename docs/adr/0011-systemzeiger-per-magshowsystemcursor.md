# ADR-0011: Systemzeiger per MagShowSystemCursor ausblenden

- **Status:** Angenommen (Sichttest durch Nutzer steht aus)
- **Datum:** 2026-10-30

## Kontext
Beim Vergrößern zeichnet ZoomIN einen eigenen, vergrößerten Zeiger (M5). Der echte Zeiger
muss währenddessen verschwinden, sonst sind zwei Zeiger zu sehen. Optionen:
- `MagShowSystemCursor(FALSE)` aus der Magnification-API
- `SetSystemCursor` mit einem leeren Zeiger (ändert die systemweiten Zeiger, bleibt nach einem
  Absturz bestehen, muss per `SystemParametersInfo(SPI_SETCURSORS)` zurückgesetzt werden)

## Entscheidung
`MagShowSystemCursor` – ausschließlich diese Zeigerfunktion der Magnification-API; die Anzeige
bleibt unser eigenes Overlay (ADR-0002 unverändert).

## Regeln
- Ausgeblendet nur, solange der Renderer des Monitors **unter dem Zeiger** einen vergrößerten
  Zeiger zeichnet (Vollbild/Lupe, Zoom > 1). Im angedockten Modus bleibt der echte Zeiger
  sichtbar, weil man im Restbereich normal arbeitet.
- Beim Ausblenden eines Monitors, beim Beenden (`Dispose`) und durch den Watchdog
  (`SystemCursorCleanup`) wird der Zeiger wieder eingeblendet.

## Konsequenzen
- Keine dauerhafte Systemänderung, kein Zurücksetzen der Zeigerschemata nötig.
- ⚠️ Ob Windows den Zeiger beim Prozessende selbst wieder einblendet, ist nicht verifiziert –
  der Watchdog-Schritt deckt beides ab.
- Animierte Zeiger (Sanduhr) werden nur mit ihrem ersten Bild vergrößert (bekannte Einschränkung).
