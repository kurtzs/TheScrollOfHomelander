---
name: lightyear-rts-networking
description: Lightyear 0.26.4 RTS networking guidance. Use when Codex works on Rusty Warfare networking, protocol registration, client/server replication, prediction, interpolation, native input, transports, content handshakes, command acknowledgement, interest management, or Lightyear source/API questions.
---

# Lightyear RTS Networking

## Grounding

Use the local Lightyear repository before making version-sensitive claims:

`C:/Users/Administrator/Documents/GitHub/lightyear`

Rusty Warfare currently depends on Lightyear `0.26.4` with `crossbeam`, `netcode`, `interpolation`, `input_native`, `prediction`, `replication`, and `udp` at workspace level; server enables `server`, `netcode`, `udp`, `websocket`, and `webtransport`.

## Networking Doctrine

- Lightyear is the main synchronization infrastructure, not a thin socket wrapper.
- Rusty Warfare owns RTS semantics: commands, content handshake, fog/visibility policy, map/package identity, and frontend projection.
- Server owns authoritative simulation. Clients submit intent and render replicated/predicted/interpolated state.
- Do not revive whole-world snapshot broadcasting as the primary sync path.

## Required Flow

```text
content lock/hash handshake
  -> client PlayerCommand intent
  -> server authoritative Bevy ECS
  -> Network* replicated components
  -> client replicated/predicted/interpolated state
  -> runtime_core frontend snapshot
  -> Godot presentation
```

## Work Checklist

When changing networking:

1. Inspect `protocol/src/shared.rs` and `protocol/src/shared/components.rs`.
2. Inspect server replication systems and client replicated-state readers.
3. Decide whether the change is a message, replicated component, input, singleton state, or diagnostics field.
4. Register protocol changes through Lightyear's protocol APIs.
5. Preserve command ids, input sequences, ticks, and acknowledgements as separate concepts.
6. Add local transport tests with deterministic stepping where possible.
7. Update frontend snapshots only after Rust-side semantics are stable.

## RTS-Specific Risks

- Interest management must avoid both bandwidth waste and hidden-information leaks.
- Prediction must reuse server movement/rule functions where practical.
- Content mismatch must reject start/connect before gameplay begins.
- Reconciliation diagnostics must explain pending, acked, rejected, replayed, and corrected commands.

## References

Read `references/lightyear-026-rts.md` for local source map and Rusty Warfare integration notes.
