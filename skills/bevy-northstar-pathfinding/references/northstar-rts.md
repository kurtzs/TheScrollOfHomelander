# bevy_northstar RTS Notes

Local source: `C:/Users/Administrator/Documents/GitHub/bevy_northstar`.

## Source Areas

- `src/grid.rs`: `Grid`, `GridSettings`, build and pathfinding entry points.
- `src/components.rs`: `AgentPos`, `Pathfind`, `NextPos`, `Blocking`.
- `src/nav.rs`: passable/impassable/portal cells.
- `src/nav_mask.rs`: layered per-request overrides.
- `src/neighbor.rs` and `src/filter.rs`: movement topology and corner rules.
- `src/hpa.rs`, `src/astar.rs`, `src/thetastar.rs`: algorithms.

## Rusty Warfare Adapter Target

Add a pathfinding adapter only after content exposes enough normalized data:

- map dimensions and tile size
- terrain type per logic tile
- movement profile passability
- collision/footprint data
- dynamic blocking policy

Then create server-side path request/result systems. Godot can draw debug paths from snapshots, but server owns path computation.
