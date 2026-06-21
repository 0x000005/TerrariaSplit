# Manual Smoke Checklist

Use this checklist after refactor steps that touch UI, configuration, storage, Terraria integration, or automation behavior.

## Startup And Overlay

- Launch the app.
- Main overlay appears without a border.
- Timer overlay appears when enabled.
- Main overlay and timer overlay remain topmost according to the setting.
- Overlay dragging works.
- Timer overlay dragging works.
- Timer overlay right-click menu opens.
- Mouse click-through toggle works.

## Settings

- Open settings.
- Switch between every settings page.
- Change a simple UI setting and apply.
- Close and reopen settings; the value is preserved.
- Change active settings profile.
- Restart the app; the active profile is remembered.
- Invalid route edits show a user-facing error and do not silently save.

## Run Control

- Pause and resume work from UI command.
- Reset works with the configured sound behavior.
- Practice mode editable time still works.
- PB update confirmation appears when expected.
- Statistics window opens and shows run data.

## Terraria Absent

- Start the app while Terraria is not running.
- Watcher status reports waiting state.
- No repeated exception dialogs or log spam appear.

## Terraria Present

- Start Terraria and let the app attach.
- Watcher detects process and menu/world state.
- Window probe diagnostics update.
- UI scale patch protection does not crash if the process exits.

## Automation

- Start create-world automation.
- Cancel create-world automation.
- Open practice-world selector.
- Cancel practice-world selection.
- Start enter-world automation from a configured slot.
- Cancel enter-world automation.

## Pyramid Filter

- Enable pyramid seed pre-screen.
- Verify unsupported options fall back safely.
- Verify final `.wld` scan still runs after world creation.
