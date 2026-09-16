---
name: bevy-ecs-runtime
description: Bevy 0.18.1 ECS runtime guidance. Use when Codex works on Bevy apps, plugins, schedules, resources, systems, headless simulation, fixed tick, ECS components, queries, tests, or Rusty Warfare server/client/runtime_core code that depends on Bevy.
---

# Bevy ECS Runtime

## Grounding

Use the local Bevy repository before making version-sensitive claims:

`C:/Users/Administrator/Documents/GitHub/bevy`

Rusty Warfare currently depends on Bevy `0.18.1` with `default-features = false`. Check the project `Cargo.toml` before assuming features or API availability.

## Runtime Principles

- Treat Bevy as Rusty Warfare's headless gameplay runtime, not its renderer.
- Prefer explicit plugins, resources, components, system sets, and fixed-tick schedules.
- Keep systems small enough to name one rule or state transition.
- Keep Bevy adapters out of pure data crates such as `content`.
- Use tests that manually step `App` when validating server/client behavior.

## System Design

When adding or refactoring systems:

1. Identify the schedule and ordering requirements.
2. Name state resources and components precisely.
3. Keep command validation separate from state mutation when practical.
4. Use `ParamSet` for overlapping query access.
5. Avoid hidden global state and ad hoc singleton lookups.
6. Add a focused test if behavior is part of authoritative simulation.

## Rusty Warfare Bevy Layers

- `server`: authoritative world and rule systems.
- `client`: replicated state readers and client-side support state.
- `runtime_core`: app lifecycle/orchestration and frontend projection.
- `content`: no Bevy dependency unless a deliberate adapter boundary is introduced outside the pure data model.

## Validation Commands

Prefer narrow checks while iterating:

```powershell
cargo fmt --check
cargo check -p server
cargo test -p server
cargo test -p runtime_core
```

Use `cargo test --workspace` after cross-crate contract changes.

## References

Read `references/bevy-018-runtime.md` for local source locations and Rusty Warfare integration notes.
