# TerrariaSplit

<p align="center">
  <span>English</span> ·
  <a href="README.md">简体中文</a>
</p>

TerrariaSplit is a highly customizable BOSS-only timer for 1.4.5 with polished split animations, flexible reference data, statistics comparison, and a full visual settings UI.

## Features

### ⚡ Convenience

TerrariaSplit aims to feel ready the moment it opens. Startup is fast, the main overlay stays lightweight, and most common settings can be changed through a visual configuration window instead of repeatedly editing JSON by hand.

### 🧩 Freedom

You can shape the timer around your own route and overlay style:

- Fully customize BOSS order, enabled states, grouping, and icons.
- Maintain multiple configurations and switch them from the main window context menu.
- Adjust the position, size, font, opacity, shadow, outline, and visibility of split display and main timer components.
- Customize UI colors to match your stream or recording layout.
- Configure sound effects for pause, reset, and split events.

### 📊 Practicality

TerrariaSplit automatically records run data and can optionally update personal best data. The statistics view makes it easy to compare your current or historical results against personal bests and reference times, helping you identify which BOSS or segment needs work.

Practice runs that do not start from the first BOSS are ignored automatically, so mid-run practice does not accidentally pollute personal best data.

To reduce Terraria-specific setup grind, the timer also includes:

- A one-key Create World workflow for faster resets and boss practice.
- Practice save slots for quickly switching between different training setups.
- pyramid filtering and Zenith auto star catch to reduce world-generation grind.

### ✨ Visual Polish

TerrariaSplit focuses on visual feedback as much as raw timing:

- BOSS icons light up progressively according to the route.
- Early delta display can show pace gain or loss before the next BOSS is down.
- Delta and timer colors can shift dynamically with the current pace, making swings easier to read at a glance.
- BOSS defeats trigger a polished completion animation with centered segment and split times, plus optional reference comparisons.
- Strong segment or overall results can use highlighted, neon, aurora, and rainbow-style text effects.
- The current stage can be emphasized so your eye naturally lands on the active split.

### 🖥️ Display Support

TerrariaSplit also includes options for modern display setups:

- Higher polling and refresh settings keep the timer smoother on high-refresh-rate monitors.
- An optional Terraria UI scale patch raises the in-game UI scaling limit from 200% to 300% on high-resolution displays.

## Preview

### Main Window

<p align="center">
  <img src="docs/images/image-5.png" alt="Main window" width="720">
</p>

<p align="center">
  <img src="docs/images/image-4.png" alt="Main window" width="720">
</p>

### BOSS Defeat Animation and Highlighted Text

<p align="center">
  <img src="docs/images/image-3.png" alt="Split completion animation" width="720">
</p>

### Current Stage Highlight

<p align="center">
  <img src="docs/images/image-6.png" alt="Current stage highlight" width="720">
</p>

## Default Hotkeys

| Action | Default key |
| --- | --- |
| Pause / Resume | `F12` |
| Reset at menu | `F6` |
| Mouse passthrough | `F9` |
| Create world at menu | `F7` |
| Load world at menu | `F8` |

## Configuration Files

TerrariaSplit stores configuration under the application directory:

On first launch, if no valid configuration exists under `Settings/`, TerrariaSplit generates one from the bundled default template.

## Notes

- The project depends on Terraria memory layout for the currently supported game version. Game updates may require rework.
- Sound effects currently use `.wav` files.
- Configuration files are plain JSON. Manual editing is supported, but keeping a backup is recommended.
- If OBS Window Capture shows a black background, set the capture-visible key color in `Settings > Colors > UI Colors > Capture background`, for example `#FF00FF` or `#00FF00`, then key that color out in OBS. The app window itself stays transparent.
## Notes

- This project was primarily built with AI assistance.

## Acknowledgements

- Thanks to [LiveSplit](https://github.com/LiveSplit/LiveSplit) for inspiring the speedrun timer layout and split display style.
- The BOSS highlight design and icon presentation were inspired by [kengho/terraria-boss-checklist](https://github.com/kengho/terraria-boss-checklist).

## OBS Capture Black Background

If OBS window capture shows a black background, try using the Windows 10 capture method instead of the traditional method.

## License

This project is licensed under the MIT License.
