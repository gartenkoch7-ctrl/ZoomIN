# ADR-0015: Versionsordner und Link „current“ in Program Files

- **Status:** Angenommen (ergänzt ADR-0014)
- **Datum:** 2026-11-03

## Kontext
Ein Update aus der App muss Dateien in `Program Files\ZoomIN` ersetzen, während genau diese
Dateien laufen. Dafür braucht es Admin-Rechte, und der Prozess mit Admin-Rechten kann keine
UIAccess-EXE aus einem Temp-Ordner sein: Windows startet UIAccess-Programme nur aus geschützten
Ordnern.

## Entscheidung
```
C:\Program Files\ZoomIN\
  app-1.0.0\     eine Version pro Ordner
  app-1.1.0\
  current  →  app-1.1.0     Verzeichnis-Link (symbolisch) auf die aktive Version
```
- Startmenü, Autostart und Neustart nutzen immer `current\ZoomIN.exe`. Kein eigener Starter.
- Update: Die App lädt und prüft das Paket als Benutzer und startet dann die **installierte**
  `current\ZoomIN.exe --apply-update <paket> <sha256> <version>` mit Admin-Abfrage. Dieser Schritt
  kopiert das Paket nach `Program Files`, prüft dort **erneut** SHA-256 und Signatur (kein Tausch
  zwischen Prüfen und Benutzen möglich), entpackt in `app-<version>`, setzt `current` um und
  löscht alles außer aktiver und vorheriger Version.
- Danach startet die App `current\ZoomIN.exe --wait-for <pid>` und beendet sich; die neue
  Version wartet, bis die alte weg ist (sonst würde die Einzelinstanz-Prüfung sie beenden).

## Verworfen
- **Dateien an Ort und Stelle ersetzen:** gesperrte Dateien, Abbruch = kaputte Installation,
  zusätzliches Hilfsprogramm ohne UIAccess nötig.
- **Eigener Starter (Squirrel/Velopack-Stil):** braucht eine weitere EXE ohne .NET-Abhängigkeit;
  der Link leistet dasselbe. Velopack selbst installiert nur pro Benutzer, ohne UIAccess.

## Konsequenzen
- Zurück zur vorherigen Version ginge ohne Download (Ordner liegt noch da) – noch nicht genutzt.
- UIAccess funktioniert über den Link (gemessen: Overlay in Band 2).
- Ein normaler Prozess darf einen UIAccess-Prozess nicht beenden („Zugriff verweigert“): Skripte
  brauchen dafür Admin-Rechte; die App beendet sich nach dem Update selbst.
- Verzeichnis-Links brauchen Admin-Rechte oder den Windows-Entwicklermodus; die Link-Tests
  laufen deshalb nur in CI (Admin) vollständig.
- Die alte flache Installation (`ZoomIN.exe` direkt in `Program Files\ZoomIN`) kann sich nicht
  selbst aktualisieren; `install-uiaccess.ps1` und das Setup ersetzen sie.
