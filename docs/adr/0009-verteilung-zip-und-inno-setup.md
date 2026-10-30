# ADR-0009: Verteilung als ZIP und Inno-Setup-Installer, nur x64

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Entscheidung
- **Portable ZIP** (self-contained) und **Inno-Setup-Installer** (Startmenü, optional
  Autostart, Deinstallation inkl. Watchdog).
- **Nur x64.** ARM64 erst nach dem MVP (kein Testgerät vorhanden).
- MSIX nicht: ohne Code-Signing-Zertifikat nicht normal installierbar.
- **Keine Beta.** Stattdessen ehrliche Kennzeichnung in den Release-Notes
  („getestet“ / „ungetestet“) und eine GitHub-Issue-Vorlage für Hardware-Berichte.

## Konsequenzen
- Unsignierte EXE → SmartScreen-Hinweis; in der Doku erklären, Code-Signing (SignPath für
  OSS) bleibt Ziel nach dem MVP.
- `ZoomIN.App` baut nur `win-x64`.
