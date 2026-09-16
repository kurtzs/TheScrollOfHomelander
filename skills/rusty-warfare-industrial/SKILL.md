---
name: rusty-warfare-industrial
description: Industrial refactoring guidance for the Rusty Warfare project. Use when Codex works in C:/Users/Administrator/Documents/GitHub/rusty_warfare on architecture, roadmap, content-package design, RTS gameplay rules, crate boundaries, Bevy/Lightyear/Godot integration, code quality audits, or any large refactor intended to turn the project into a production-grade Rust-authoritative RTS.
---

# Rusty Warfare Industrial

## Operating Rule

Treat Rusty Warfare as a server-authoritative RTS platform, not a pile of gameplay scripts.

Before changing code, read the local project files that own the behavior. Prefer `rg`, `rg --files`, and focused file reads. Do not rely on stale docs, Deepseek skills, or memory when local code disagrees.

Assume architecture rot is project-wide unless proven otherwise. Do not treat `content` as the only bad area: Snapshot/DTO sprawl, root-level exports, project preludes, runtime_core god objects, server state bags, giant tests, and Godot mega-scripts are first-class refactor targets.

## Project Spine

Current workspace root: `C:/Users/Administrator/Documents/GitHub/rusty_warfare`.

Crate roles:

- `builder`: build and CI tooling. Keep it mostly untouched unless the user asks.
- `content`: pure Rust content package loading, normalization, validation, fingerprints, locks, and future mod merging. It must not depend on Godot or Bevy runtime state.
- `protocol`: server/client contract: messages, commands, replicated network components, constants.
- `server`: Bevy authoritative simulation, command validation, rules, state mutation, replication projection.
- `client`: Bevy client state collection, command submission, prediction/interpolation client-side state.
- `runtime_core`: lifecycle orchestration, app construction, frontend projection, diagnostics, local/remote mode management.
- `gdextension`: Rust-to-Godot FFI boundary and Dictionary conversion only.
- `launcher/godot`: presentation, input collection, scene/UI composition, asset display. It must not own authoritative rules.

## Mandatory Boundaries

- Godot never validates authoritative gameplay rules beyond local UI affordances.
- GDExtension never becomes a gameplay service locator; expose coarse stable API calls and typed snapshots.
- `runtime_core` orchestrates apps and frontend snapshots; do not let it become a second server.
- `server` consumes normalized content plans, not raw TOML files or ad hoc defaults.
- `content` owns raw data, normalization, validation, IDs, package locks, fingerprints, and mod merge semantics.
- `protocol` changes require simultaneous server/client/runtime/Godot snapshot review.
- Singleplayer and multiplayer must share the same authoritative server rules.
- Do not add broad project-owned preludes or root-level wildcard re-exports.
- Do not add another generic `Snapshot` type when a purpose-specific contract/view name is clearer.
- Do not use frontend DTOs as command validation or authoritative simulation input.
- Do not grow already-large modules unless the same change extracts a real owner module.

## Industrial Refactor Workflow

1. Quarantine the architecture first.
   Freeze broad exports, project preludes, new giant DTO files, and new mega-scripts. Add compatibility shims only where needed.

2. Establish the behavioral contract.
   Read current docs, then verify against code. Record where docs are stale.

3. Split data phases explicitly.
   Move content toward:

   ```text
   files -> RawContent -> normalized content -> validated ContentDatabase
     -> SpawnPlan / RulePlan / FrontendCatalog -> server/client adapters
   ```

4. Split frontend contracts explicitly.
   Move away from all-purpose `Frontend*Snapshot` bags toward `FrontendFrame`, `GameView`, `DebugView`, `CommandTimelineView`, `MapRenderModel`, `UnitView`, and similar purpose-owned contracts.

5. Protect the public boundary.
   Add tests around current behavior before replacing internals when the blast radius is high.

6. Refactor by vertical slice.
   Prefer one domain at a time: content IDs, unit schema, map schema, command flow, movement/pathing, combat, economy, frontend projection, Godot scene/UI.

7. Keep migrations visible.
   Document long-lived decisions under `docs/architecture/` or the active roadmap. Do not bury major policy in comments.

## Content Package Doctrine

Official gameplay is a package, not privileged code. Mods should later be able to replace, patch, extend, and depend on packages deterministically.

Do not add more direct runtime consumers of raw package TOML. Instead create typed intermediate objects and adapters.

Content validation should catch:

- invalid or duplicate namespaced IDs
- missing cross references
- circular templates/inheritance
- invalid resource costs and rates
- invalid map dimensions, teams, spawns, layers, terrain codes, objects
- invalid asset paths and package-relative path traversal
- schema version mismatch
- package lock/hash mismatch for multiplayer

## Game Philosophy

Design toward "industrial warfare at readable scale":

- Tactical clarity over visual noise.
- Units are defined by roles, constraints, logistics, and combined arms, not only DPS numbers.
- Terrain and resources matter: control, pathing, chokepoints, scouting, expansion.
- Multiplayer fairness depends on deterministic content handshakes and server authority.
- Modding is first-class but sandboxed by schema, load order, validation, and package locks.
- The frontend should feel responsive through prediction/interpolation, while never pretending to be authoritative.

## When More Context Is Needed

- For detailed current project policy, read `references/project-boundaries.md`.
- For concrete file-level root rot findings, read `docs/architecture/root-rot-audit.md` in the project.
- For Bevy schedule/runtime work, use `bevy-ecs-runtime`.
- For Lightyear networking work, use `lightyear-rts-networking`.
- For Godot or gdext frontend work, use `godot-rust-frontend`.
- For pathfinding integration, use `bevy-northstar-pathfinding`.
