# Godot Frontend Boundary

Rusty Warfare frontend path: `C:/Users/Administrator/Documents/GitHub/rusty_warfare/launcher/godot`.

## Current Cleanup Target

`scripts/game/game.gd` should be split. Keep the scene working while moving logic into clearer scripts:

- `snapshot_reader.gd`: safe extraction from Rust dictionaries.
- `world_renderer.gd`: map layers, objects, units, markers.
- `selection_controller.gd`: click selection and command target conversion.
- `camera_controller.gd`: movement, zoom, bounds.
- `hud_controller.gd`: status, command buttons, summaries.
- `asset_cache.gd`: package-relative images/audio.

`scripts/backend/rust_backend.gd` should remain the only normal GDScript caller of `RustyCore`.

## API Shape

Prefer stable Rust snapshot dictionaries with explicit nested sections:

- `room`
- `map`
- `resources`
- `units`
- `objects`
- `commands`
- `diagnostics`
- `content`

Godot should display missing data gracefully and avoid inventing rule defaults.
