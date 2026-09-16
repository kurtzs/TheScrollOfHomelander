# Rusty Warfare Project Boundaries

Use this reference when planning or auditing architecture.

## Current Pain Points To Watch

- `content` is close to direct-deserialize-and-consume. Move toward raw, normalized, validated, and planned phases.
- `runtime_core/src/snapshot.rs` is a crate-root-exported DTO junk drawer. Split frontend contract types by view purpose.
- `runtime_core/src/frontend_projection.rs` is an oversized projection/prediction/diagnostics assembler. Split it into view builders.
- `runtime_core/src/network_apps.rs` is a session god object. Split app ownership, command session, handshake session, and local-loop stepping.
- broad project preludes and root `pub use *` exports hide ownership and create giant `use crate::{...}` imports.
- `launcher/godot/scripts/game/game.gd` currently mixes snapshot extraction, map rendering, unit rendering, input, camera, command UI, asset loading, and diagnostics summaries.
- `launcher/godot/scripts/backend/rust_backend.gd` mixes Rust facade and diagnostics UI.
- `server/src/game/systems.rs` mixes command validation, production, movement, combat, resource economy, projectile updates, cleanup, and victory checks in one module.
- `server/src/game/state.rs` mixes unrelated domain state and registries.
- monolithic test files protect useful behavior but must be split into domain suites before deep rewrites.
- Some docs and comments show encoding damage. Verify meaning against code and rewrite durable docs in clean UTF-8/ASCII.

## Red Lines

- Do not let Godot own authoritative rules.
- Do not let content depend on Bevy/Godot for runtime rules.
- Do not let runtime_core become a second gameplay engine.
- Do not add protocol fields without reviewing server, client, runtime snapshots, gdextension, and Godot.
- Do not add raw TOML parsing inside server systems.
- Do not add new project-owned wildcard preludes or broad crate-root `pub use *`.
- Do not add new generic `Snapshot` types when a purpose-specific view/contract name is clearer.
- Do not use frontend DTOs as authoritative validation input.
- Do not make any already-large module larger unless the same change immediately extracts an owner module.

## Desired Long-Term Package Pipeline

```text
PackageSetSelection
  -> ManifestGraph
  -> RawContentBundle
  -> Patch/Replace/Extends merge
  -> NormalizedContent
  -> CrossReferenceValidation
  -> ContentDatabase
  -> PackageLock + Fingerprint
  -> SpawnPlan / RulePlan / FrontendCatalog / NavGridBuildPlan
```

## First High-Value Refactor Targets

1. Freeze broad exports/preludes and split frontend Snapshot contracts.
2. Split `runtime_core` session, command, projection, and reconciliation responsibilities.
3. Godot `game.gd` and diagnostics backend decomposition.
4. Server gameplay systems/state by domain.
5. Content phase split and validation error quality.
6. Test suite decomposition.
7. Movement/pathfinding architecture.
8. Lightyear command lifecycle and diagnostics.
