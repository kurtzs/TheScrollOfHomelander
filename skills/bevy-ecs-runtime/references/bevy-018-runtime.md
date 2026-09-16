# Bevy 0.18.1 Runtime Notes

Local source: `C:/Users/Administrator/Documents/GitHub/bevy`.

Rusty Warfare uses Bevy as an ECS app framework for server/client runtime, usually without renderer features.

## Local Source Areas

- `crates/bevy_ecs`: ECS, schedules, systems, resources, queries.
- `crates/bevy_app`: `App`, plugins, plugin groups, sub-apps.
- `crates/bevy_time`: time and fixed timestep concepts.
- `examples/ecs` and `examples/app`: practical patterns.

## Rusty Warfare Checks

- `server/src/lib.rs`: server plugin setup and schedule ordering.
- `server/src/game`: authoritative components, resources, systems, replication.
- `client/src/lib.rs`: client plugin setup.
- `runtime_core/src`: app lifecycle and stepping.

## Design Preference

Use domain plugins and named system sets once systems grow. A single chained tuple is acceptable for a tiny prototype, but production RTS code should make ordering inspectable by domain.
