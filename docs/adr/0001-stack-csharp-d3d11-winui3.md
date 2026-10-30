# ADR-0001: C# / .NET 10 + Direct3D 11 (Vortice) + WinUI 3

- **Status:** Angenommen
- **Datum:** 2026-10-30

## Kontext
Anforderungen: maximal flüssiger Zoom, geringe Latenz, gut gestaltete Desktop-App,
Open Source, Windows only. Alternativen: C++-Kern + C#-UI, komplett C++.

## Entscheidung
Alles in C# (.NET 10 LTS). GPU-Zugriff über Vortice.Windows (D3D11/DXGI), UI mit WinUI 3.

## Begründung
- Die Arbeit pro Frame passiert auf der GPU; die CPU setzt nur wenige Aufrufe ab. Die Sprache
  entscheidet daher kaum über Bildrate oder Latenz.
- Eine Sprache = einfacher Build, keine Interop-Schicht, leichter für Mitwirkende.
- WinUI 3 bietet modernes Fluent Design; UI Automation ist in .NET gut erreichbar.

## Konsequenzen
- GC-Pausen sind das Hauptrisiko → Render-Loop ohne Allokationen, eigener Thread
  (AGENTS.md 2.3), Benchmarks mit `0 B`.
- Render-Kern hinter Core-Interfaces → bei messbaren GC-Problemen nach C++ portierbar,
  ohne den Rest zu ändern.
