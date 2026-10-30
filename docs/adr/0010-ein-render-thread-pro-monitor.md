# ADR-0010: Ein Render-Thread und ein GPU-Device pro Monitor

- **Status:** Angenommen (Messung mit zweitem Monitor steht noch aus)
- **Datum:** 2026-10-30

## Kontext
Mehrere Monitore können unterschiedliche Bildwiederholraten (60/144 Hz), Auflösungen, DPI
und sogar verschiedene Grafikadapter haben. Optionen: ein Render-Thread für alle Monitore
(wartet auf den schnellsten Monitor) oder ein Thread pro Monitor.

## Entscheidung
Pro Monitor ein `MonitorRenderer` mit eigenem Thread, eigenem D3D11-Device (auf dem Adapter
des Monitors), eigener Desktop Duplication, eigenem Overlay und eigener Swapchain.
`ZoomEngine` startet die Renderer und baut bei jeder Anzeigeänderung alle neu auf.

## Begründung
- Jeder Monitor wird von seiner eigenen Swapchain im eigenen Takt gesteuert – kein Monitor
  wartet auf einen anderen, 60 und 144 Hz laufen gleichzeitig sauber.
- Desktop Duplication muss ohnehin auf dem Adapter des Monitors laufen (Hybrid-Grafik).
- Fehler (Device Removed, Access Lost) bleiben auf einen Monitor begrenzt.
- Einfacher Code: Die Ein-Monitor-Logik aus M1/M2 bleibt unverändert, nur vervielfacht.

## Konsequenzen
- Etwas mehr GPU-Speicher (ein Device + Kopie-Textur je Monitor) – bei 2–3 Monitoren unkritisch.
- Monitor-ID ist der Windows-Gerätename (`\\.\DISPLAYn`), identisch in DXGI und GDI.
- Anzeigeänderung → alle Renderer neu (nach 500 ms Beruhigungszeit), statt einzeln abzugleichen.
- Gibt es keinen Monitor mit Pipeline, erkennt ZoomIN Anzeigeänderungen nicht (bekannte Lücke).
