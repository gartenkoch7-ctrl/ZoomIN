# ZoomIN – User manual

## Installation
- **Setup** (`ZoomIN-Setup.exe` from the GitHub release): an assistant guides you through five
  steps – language, channel and version, settings, installation, done. It downloads the chosen
  version, checks its checksum and signature and installs it to `C:\Program Files\ZoomIN`. Windows
  asks once for administrator rights.
- Only the installed copy may magnify **on top of other windows** such as the Start menu or the volume
  and network flyouts, and only it runs on the sign-in and lock screen.
- Settings in the assistant (all preselected): **Start with Windows**, **Also on the sign-in and lock
  screen**, desktop shortcut, check for updates regularly.
- An older installation (including the former installer) is replaced; your settings stay.
- **ZIP**: unpack and run `ZoomIN.exe`. Nothing is installed. This copy cannot magnify over the Start
  menu and system flyouts and shows a notice with a button that starts the setup.
- Requires Windows 10 version 2004 or later, 64-bit.
- On first start an **assistant** walks you through the essentials in five steps (skipped when the
  setup has already set everything up).

## Updates
- **Settings → Updates** shows the installed version and the available versions.
- **Channel:** "Stable" shows finished versions only. "Experimental" adds release candidates and
  developer builds – untested, they may have bugs.
- **Update** or **go back** to an older version: one click, then confirm the Windows prompt. ZoomIN
  restarts afterwards. Before going back, settings and profiles are backed up.
- A download that is damaged or not signed by ZoomIN is discarded and nothing changes.
- With "check for updates regularly" ZoomIN checks at start and once a day and reports a new version
  in the notification area.

## Uninstall
- **Windows Settings → Apps → Installed apps → ZoomIN → Uninstall.**
- ZoomIN asks whether your settings and profiles should stay. Windows then asks for administrator
  rights.
- Removed are the program, Start menu and desktop shortcuts, the autostart (for all user accounts)
  and the registration for the sign-in and lock screen.

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
| Ctrl+Alt+K | Crosshair on/off |

Filters: inverted · inverted keeping hue · greyscale · greyscale inverted · yellow on black ·
black on yellow · white on blue. Contrast and brightness are under **Colour filters**, smoothing
under **Zoom & modes**.

With "Separate colour filter per monitor" (on by default) each monitor remembers whether its filter
is on and which one; the shortcuts then act on the monitor under the mouse. On the **Colour
filters** page you pick the monitor from a list; "Default" applies to all monitors without their
own filter. Contrast and brightness always apply to all monitors.

The **Mouse pointer** page (with live preview) sets: "Override Windows pointer settings" (ZoomIN
then always draws the standard Windows pointer in its own size and colour; pointer size and colour
from the Windows settings no longer apply), size without zoom (1–4×), "Size relative to zoom" (on:
size × zoom factor up to the maximum size; off: the pointer keeps its size) and the maximum size
while zoomed, colour (original, tint, inverted), contrast rim with colour and width, the locator
(colour, size, duration; triggered by shortcut, shaking the mouse, after a 3-second pause or Ctrl
alone), the crosshair (only while zoomed or always) and "hide pointer while typing". The
**Overview** start page gathers zoom on/off, mode, level, quick switches, profile and status.

## Keyboard focus and text cursor
Press Tab and the zoom moves to the focused element (magenta frame). While typing it follows
the text cursor once it gets close to the edge. Move the mouse and the zoom glides back to it.
**Ctrl+Alt+T** switches following on and off.

## Profiles
A profile stores the display (mode, levels, filter, pointer, focus tracking). Shortcuts are
shared by all profiles. **Ctrl+Alt+P** switches to the next profile. Profiles can be exported
and imported on another computer.

## Sign-in and lock screen
- With "Also on the sign-in and lock screen" (Settings → General) ZoomIN also magnifies the sign-in,
  the lock screen (Win+L), Ctrl+Alt+Del and Windows prompts for administrator rights – with your zoom
  level and colour filter.
- Islands, freezing and switching monitors are not available there. **Ctrl+Alt+Shift+Z** turns
  ZoomIN off there until the next screen change.
- Changing the switch asks for administrator rights.

## Screen sharing
When you share your screen in Teams, Zoom, Discord or a browser, others see your normal desktop
– only you see the zoom. Exception: devices that tap the video signal directly (projector in
mirror mode, capture card) show the zoom.

## Troubleshooting
- **Shortcut does nothing:** another application uses it – change it under "Shortcuts".
- **ZoomIN crashed:** it restarts automatically (at most three times in five minutes).
- **Log files:** Settings → Info & status → Open log folder.
- **Reset settings:** exit ZoomIN, delete `%AppData%\ZoomIN\settings.json`.
