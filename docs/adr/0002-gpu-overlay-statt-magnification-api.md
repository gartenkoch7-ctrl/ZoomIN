# ADR-0002: Eigenes GPU-Overlay statt Windows-Magnification-API

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Kontext
Der Zoom muss bei Bildschirmübertragungen (Teams usw.) unsichtbar sein. Die eingebaute
Magnification-API (`MagSetFullscreenTransform`, Magnifier-Control) verändert die
Desktop-Ausgabe selbst und lässt sich nicht aus Aufnahmen ausschließen. Außerdem erlaubt sie
keine eigenen Shader (Glättung, Filter).

## Entscheidung
Desktop per DXGI Desktop Duplication erfassen, auf der GPU vergrößern und in ein eigenes
Overlay-Fenster mit `WDA_EXCLUDEFROMCAPTURE` zeichnen.

## Konsequenzen
- ✅ Unsichtbar in Software-Aufnahmen, keine Rückkopplung beim eigenen Capture.
- ✅ Volle Kontrolle über Qualität, Filter, Animation.
- ⚠️ Mindestens Windows 10 2004.
- ⚠️ Wir müssen Fehlerfälle (Access Lost, Device Removed, Monitorwechsel) selbst behandeln.
- ⚠️ Klick-Mapping muss selbst gelöst werden → ADR-0003.
