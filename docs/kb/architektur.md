# Architektur

## Projekte
```
ZoomIN.sln
src/
  ZoomIN.Core/          Reine Logik, keine Plattformabhängigkeit
  ZoomIN.Platform/      Win32, UI Automation, Hooks, Monitore, DPI, Hotkeys
  ZoomIN.Rendering/     D3D11, Desktop Duplication, Shader, Overlay-Swapchain
  ZoomIN.App/           WinUI 3, Tray, Einstellungen, Composition Root
tests/
  ZoomIN.Core.Tests/
  ZoomIN.Architecture.Tests/
  ZoomIN.Benchmarks/
```

## Abhängigkeitsregeln
| Projekt | darf referenzieren |
|---|---|
| Core | – (nur BCL) |
| Platform | Core |
| Rendering | Core |
| App | Core, Platform, Rendering |
| Speech (später) | Core |

Platform und Rendering sprechen nur über Interfaces aus Core miteinander; verbunden in App.

## Ordnerstruktur (nach Feature)
```
ZoomIN.Core/
  Zoom/          ZoomState, ZoomEasing, ZoomLimits
  Viewport/      ViewportCalculator, PhysicalPoint, PhysicalRect
  Modes/         IZoomMode, FullscreenMode, LensMode, DockedMode
  Tracking/      ITrackingSource, TrackingArbiter (wer gewinnt: Maus/Fokus/Caret)
  Filters/       ColorFilterSettings, ColorMatrix
  Settings/      AppSettings, Profile, ISettingsStore
  Monitors/      MonitorInfo, IMonitorProvider
ZoomIN.Platform/
  Interop/       P/Invoke, CsWin32
  Input/         MouseTracker, Hotkeys
  Automation/    FocusTracker, CaretTracker (UIA)
  Monitors/      Win32MonitorProvider, DPI
  Settings/      JsonSettingsStore
ZoomIN.Rendering/
  Interop/
  Capture/       DesktopDuplicator (pro Monitor)
  Overlay/       OverlayWindow (click-through, Capture-Ausschluss)
  Gpu/           Device, Swapchain, Shader-Pipeline
  Shaders/       *.hlsl
  RenderLoop/    RenderThread, FrameScheduler
ZoomIN.App/
  Composition/   DI-Setup
  Tray/
  Views/         Settings-Seiten (XAML)
  ViewModels/
```

## Datenfluss pro Frame
```
Platform (Maus/Fokus/Caret) ─► Core.TrackingArbiter ─► Core.ViewportCalculator
                                                             │
                                                    RenderSnapshot (immutable)
                                                             ▼
Rendering: DesktopDuplicator ─► Shader (Scale + Filter + Cursor) ─► Overlay-Swapchain
```
- UI ändert Settings → neuer `RenderSnapshot` → atomarer Referenztausch.
- Render-Thread liest pro Frame genau einen Snapshot; keine Locks.

## Muster
- DI über `Microsoft.Extensions.DependencyInjection`, nur in App konfiguriert.
- MVVM in App (`CommunityToolkit.Mvvm`).
- Ereignisse aus Platform werden als Werte (structs) weitergereicht, nicht als Objekte.
