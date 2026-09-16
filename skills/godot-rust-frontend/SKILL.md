---
name: godot-rust-frontend
description: Godot 4 and godot-rust frontend guidance for Rusty Warfare. Use when Codex works on launcher/godot GDScript, scenes, UI, presentation-only rendering, resource loading, diagnostics panels, gdextension Dictionary conversion, RustyCore APIs, or Godot/Rust boundary cleanup.
---

# Godot Rust Frontend

## Grounding

Relevant local repositories:

- Rusty Warfare Godot project: `C:/Users/Administrator/Documents/GitHub/rusty_warfare/launcher/godot`
- godot-rust source: `C:/Users/Administrator/Documents/GitHub/gdext`

Verify the actual Godot project files and Rust `gdextension` code before changing APIs or scene assumptions.

## Boundary Rule

Godot is presentation and input collection. Rust is authoritative gameplay.

Godot may:

- display frontend snapshots
- collect local input and submit command dictionaries
- show diagnostics, menus, lobby state, and user feedback
- load visual/audio assets referenced by validated content

Godot must not:

- decide authoritative combat, economy, production, pathing, or victory rules
- parse content package rules as a second runtime
- infer server truth from local scene nodes
- bypass `RustBackend` to call `RustyCore` all over the project

## Refactor Pattern

Split large GDScript files into presentation services:

- `RustBackend`: only Rust API calls, result normalization, diagnostics access.
- Snapshot model helpers: typed extraction from Dictionary with defaults.
- World renderer: units, map layers, objects, markers.
- Input controller: camera, selection, command issue.
- HUD/diagnostics views: labels, panels, filters.
- Asset resolver/cache: package-relative visual resources.

Keep scenes stable while moving logic into smaller scripts.

## GDExtension Rule

Rust `gdextension` should expose stable coarse methods and convert Rust typed DTOs to Godot-safe Dictionary/Array values. Avoid passing Godot objects into Rust gameplay. Use explicit error dictionaries at the boundary.

## Validation

For Rust boundary changes:

```powershell
cargo fmt --check
cargo check -p gdextension
cargo test -p runtime_core
```

For Godot changes, run/open the Godot project when feasible and inspect the scene behavior.

## References

Read `references/frontend-boundary.md` for the Rusty Warfare Godot cleanup target.
