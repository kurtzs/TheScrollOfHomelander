---
name: bevy-northstar-pathfinding
description: bevy_northstar pathfinding guidance for RTS movement. Use when Codex works on Rusty Warfare pathfinding, terrain passability, movement profiles, nav grids, HPA*, dynamic blocking, path requests, Bevy integration, or local bevy_northstar source/API questions.
---

# Bevy Northstar Pathfinding

## Grounding

Use the local repository before making API claims:

`C:/Users/Administrator/Documents/GitHub/bevy_northstar`

The local crate is `bevy_northstar` `0.6.2`, built for Bevy `0.18`.

## Role In Rusty Warfare

Use Northstar as a pathfinding service below authoritative server movement. It should consume validated map terrain/pathing data and movement profiles; it should not parse content files directly.

Target adapter flow:

```text
ContentDatabase / MapTemplate / MovementProfile
  -> NavGridBuildPlan
  -> Bevy Northstar Grid
  -> server path requests
  -> movement intents / path-following components
  -> replicated NetworkPosition / NetworkMovementTarget / diagnostics
```

## Integration Rules

- Build nav grids from normalized content, not Godot TileMap state.
- Keep grid coordinates, world coordinates, tile size, and layer IDs explicit.
- Rebuild or patch grids only when authoritative terrain/blocking changes.
- Use movement profiles and nav masks for land/air/water/hover constraints.
- Treat dynamic unit avoidance separately from static terrain pathfinding.
- Keep pathfinding deterministic enough for server authority; clients may visualize paths but not author them.

## Work Checklist

1. Read current `content/src/world.rs`, `content/src/map.rs`, and server movement systems.
2. Define the data adapter before adding Northstar components.
3. Write tests for blocked terrain, out-of-bounds requests, profile-specific passability, and no-path cases.
4. Only then integrate path-following into server movement.
5. Replicate high-level movement state, not entire internal pathfinder structures.

## References

Read `references/northstar-rts.md` for local source map and Rusty Warfare pathfinding plan.
