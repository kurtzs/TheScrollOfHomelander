# Lightyear 0.26.4 RTS Notes

Local source: `C:/Users/Administrator/Documents/GitHub/lightyear`.

## Source Areas

- `lightyear/src/client.rs`, `lightyear/src/server.rs`, `lightyear/src/shared.rs`: plugin groups.
- `lightyear_replication`: replicated entities/components, send/receive, visibility.
- `lightyear_prediction`: rollback and prediction.
- `lightyear_interpolation`: smoothing for remote state.
- `lightyear_inputs` and `lightyear_inputs_native`: input buffering.
- `lightyear_crossbeam`: deterministic local testing transport.
- `lightyear_tests`: integration patterns.

## Rusty Warfare Areas

- `protocol/src/shared.rs`: channels, messages, protocol plugin.
- `protocol/src/shared/components.rs`: replicated component contract.
- `server/src/game/network.rs`: projection from authoritative ECS to Network* components.
- `client/src/network.rs`: replicated state reads.
- `runtime_core/src/frontend_prediction.rs`, `frontend_reconciliation.rs`, `frontend_projection.rs`: current frontend support layer.

## RTS Networking Target

Use Lightyear for infrastructure, but keep RTS policy explicit:

- room and content handshakes
- selected map and active mods
- command ids and input sequences
- server ticks and acknowledgement
- visibility and interest management
- reconnect and spectator policy
