# ZoomIN – User manual

## Installation
- **Installer** (`ZoomIN-…-setup.exe`): installs for your account, no admin rights, Start menu
  entry, optional "Start with Windows".
- **ZIP**: unpack and run `ZoomIN.exe`. Nothing is installed.
- Requires Windows 10 version 2004 or later, 64-bit.
- On first start an **assistant** walks you through the essentials in five steps.

ZoomIN then lives in the **notification area** (next to the clock, possibly under "^"):
- **Click** the magnifier icon: settings
- **Right-click**: zoom on/off, mode, next profile, **Exit ZoomIN**
- The settings window's X does not exit ZoomIN; it keeps running in the notification area.

## Zooming
| Shortcut | Action |
|---|---|
| Ctrl+Alt+Z | Zoom on/off (in the last used mode) |
| Ctrl+wheel | Zoom in/out smoothly; back to 1× = off |
| Ctrl+Alt+Page Up / Page Down | Zoom in/out in steps |

**Note:** Ctrl+wheel belongs to ZoomIN. Browsers and Office no longer zoom with it – you can
change this under "Shortcuts".

## Modes
| Shortcut | Mode |
|---|---|
| Ctrl+Alt+F | **Full screen** – the whole monitor is magnified |
| Ctrl+Alt+L | **Lens** – a rectangle around the pointer; resize with Ctrl+Alt+Shift+arrow keys |
| Ctrl+Alt+D | **Docked** – a strip (default: top, 40 %) shows the area around the pointer; other windows move into the rest |

Pressing the same shortcut again switches the zoom off. Each mode keeps its own level, and by
default each monitor has its own level.

**Clicking while zoomed:** in full screen and lens, just click what you see. The docked strip
is display-only.

## Colours and pointer
| Shortcut | Action |
|---|---|
| Ctrl+Alt+C | Colour filter on/off (also without zoom) |
| Ctrl+Alt+Shift+C | Next filter |
| Ctrl+Alt+S | Locator: a ring shrinks onto the pointer |

Filters: inverted · inverted keeping hue · greyscale · greyscale inverted · yellow on black ·
black on yellow · white on blue. Contrast, brightness, pointer size and smoothing are under
**Colours & pointer** and **Zoom & modes**.

## Keyboard focus and text cursor
Press Tab and the zoom moves to the focused element (magenta frame). While typing it follows
the text cursor once it gets close to the edge. Move the mouse and the zoom glides back to it.
**Ctrl+Alt+T** switches following on and off.

## Profiles
A profile stores the display (mode, levels, filter, pointer, focus tracking). Shortcuts are
shared by all profiles. **Ctrl+Alt+P** switches to the next profile. Profiles can be exported
and imported on another computer.

## Screen sharing
When you share your screen in Teams, Zoom, Discord or a browser, others see your normal desktop
– only you see the zoom. Exception: devices that tap the video signal directly (projector in
mirror mode, capture card) show the zoom.

## Troubleshooting
- **Shortcut does nothing:** another application uses it – change it under "Shortcuts".
- **ZoomIN crashed:** it restarts automatically (at most three times in five minutes).
- **Log files:** Settings → Info & status → Open log folder.
- **Reset settings:** exit ZoomIN, delete `%AppData%\ZoomIN\settings.json`.
