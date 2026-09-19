---
name: taiwu-mod-authoring
description: Build, update, review, debug, and package The Scroll of Taiwu Mods against the currently installed game. Use for C# frontend/backend plugins, Unity UI and Harmony patches, GameData domain rules, Config.lua or Settings.Lua, frontend/backend method bridges, reference-Mod analysis, game-update compatibility fixes, Roslyn-worker builds, deployment, and Player.log verification.
---

# Taiwu Mod Authoring

Treat a Taiwu Mod as a versioned runtime product. Preserve vanilla behavior when the feature is disabled, keep frontend and backend assemblies separate, and verify the exact DLL that the game will load.

## Delivery Workflow

1. **Inventory the active installation.** Resolve the game root, frontend `Managed` directory, backend directory, Mod root, and the actual user-data root. Inspect the target Mod's `Config.lua`, plugin paths, current version, existing changes, and both frontend/backend DLLs before editing. Never assume the Steam path or that AppData and Steam contain the same copy.
2. **Classify the feature.** Put Unity/UI/input/assets in frontend; authoritative item, character, world, combat, and save-state rules in backend; split intent from authority when both are needed. Do not reference frontend Unity assemblies from backend code or copy game assemblies into the Mod.
3. **Read the installed API.** Search by exact type/member, then inspect a narrow source span from the current DLL, using `TaiwuStudio.DecompilerWorker` (see the `taiwu-decompiled-api` skill). Never decide a patch target from an old decompiled dump. Record side, assembly, full type, overload parameters, and method token.
4. **Design state and contracts first.** Define configuration keys, defaults, ownership of mutable state, lifecycle reset points, and versioned frontend/backend request and response fields before writing patches. Keep persistent JSON backward-compatible; normalize once at load and migrate only with an explicit reason.
5. **Implement the smallest safe hook.** Prefer Postfix or non-skipping Prefix. Use a skipping Prefix only when the replacement fully preserves required outputs and side effects. Guard destroyed/inactive Unity objects, scene rebuilds, duplicate controls, stale async responses, and repeated initialization. Put expensive work behind state changes, not `Update`/`LateUpdate` scans.
6. **Build with the project toolchain.** Use the `TaiwuStudio.RoslynWorker` console app and side-specific game libraries; a Mod-local `.csproj` is not the build path. Compile frontend and backend separately, always including the shared source directory, and treat every error as a build failure even if warnings are tolerated.
7. **Package without collateral changes.** Copy only declared DLL/PDB, `Config.lua`, assets, and required data files to the actual Mod paths. Preserve user-edited Config values and unrelated files; do not deploy `bin`, `obj`, or game DLLs. Read [current-build-and-deploy.md](references/current-build-and-deploy.md) for the exact commands and hash checks.
8. **Restart and verify runtime behavior.** A loaded Unity process keeps the old assembly. Require a full game restart for DLL changes, then inspect `Player.log` for plugin load, Harmony target failures, exceptions, and the feature's summary logs. Test enabled and disabled paths, repeated open/refresh/scene transitions, and at least one no-op/invalid-input case.

## Side Boundaries

| Side | Use | Typical references |
| --- | --- | --- |
| Frontend | Unity objects, views, controls, input, sprites, display-only state | `Assembly-CSharp.dll`, `TaiwuModdingLib.dll`, `0Harmony.dll`, Unity modules |
| Backend | authoritative domains, validation, mutations, deterministic rules | `GameData.dll`, `GameData.Shared.dll`, `TaiwuModdingLib.dll`, `0Harmony.dll` |
| Split | frontend collects intent; backend validates and applies it | explicit `SerializableModData` contract |

Keep shared code limited to side-neutral constants, DTOs, and codecs. If a type exists in both trees, still verify its assembly and behavior on the intended side.

## Harmony Rules

- Target the exact installed method. Disambiguate overloads with the parameter array or metadata token; use `MethodType.Constructor` for constructors.
- Prefer stable lifecycle methods (`Awake`, `OnInit`, `Refresh`, `OnEnable`) and domain/action methods over broad per-frame hooks.
- Use `Postfix` for additive UI or result changes, a non-skipping `Prefix` for argument preparation, and a `Finalizer` only for deliberate exception containment.
- Do not mutate a collection while vanilla code enumerates it. Defer UI layout work until the owning object is active and coalesce same-frame refreshes.
- Cache `FieldInfo`/`MethodInfo`/component references per type or instance. Never create `Traverse`, search the whole hierarchy, or write settings in a hot loop.
- Make patches idempotent. Check for existing controls, listeners, subscriptions, and Harmony IDs before adding them; unpatch and dispose everything created by the plugin.

## Settings And Persistence

- Put user-facing toggles in the existing Config group that owns the feature. Do not silently move unrelated options between groups; in particular keep month/advance-month optimization separate from in-game performance optimization.
- Load settings once, apply them in memory immediately, and save only on genuine user changes or lifecycle shutdown. Use an immutable snapshot and atomic write for asynchronous saves.
- Keep per-view/per-building/per-item state keyed by stable identity, not one global static value. Distinguish UI replay from a real user action so initialization cannot overwrite memory with “All”.
- When a game update changes a private or synthetic UI state, include the effective state in signatures and equality checks, then restore it after vanilla refresh callbacks complete.

## Frontend/Backend Bridges

Use `DomainManager.Mod.AddModMethod` for ordinary frontend-triggered gameplay actions. Define constants for method and field names. Requests should include `version`, `requestId` when asynchronous, explicit scope/target IDs, and all switches that affect behavior. Responses should always include `success`, `code`, `message`, and structured payload fields.

The backend is authoritative: re-fetch current objects, validate ownership/source/quantities/preconditions, and use existing domain APIs for mutation. Reject stale or malformed requests without changing state. The frontend must handle failure, timeout, panel closure, and out-of-order responses.

## Update Compatibility

When a game update breaks a patch, do not guess from an old source dump. Re-resolve the current method with `TaiwuStudio.DecompilerWorker`, compare fields and callbacks, and trace the full lifecycle from UI input through data refresh and final render. Common symptoms include `Undefined target method`, `InvalidCastException` from changed private fields, UI controls appearing only after a toggle, and a visible filter differing from the list controller's effective filter. Use [update-compatible-debugging.md](references/update-compatible-debugging.md).

## Performance And Safety

- Start with a measured hot path and keep a before/after observation in the plan or log. Do not bundle unrelated behavior changes into an optimization patch.
- Replace repeated scans with event-driven invalidation, per-instance caches, and one coalesced refresh. Keep one-frame delayed UI work null-safe and cancel it when the view closes.
- Avoid synchronous file I/O, reflection, global object scans, and success logs in render/list/tick paths.
- Preserve vanilla behavior when the feature is disabled or when an API lookup, backend call, asset, or saved value is invalid. A safe no-op is preferable to consuming resources or skipping the game's original action.

## Verification Checklist

- Compile the intended side(s) with zero errors and record output DLL/PDB paths.
- Confirm `Config.lua` declares exactly those paths and that version/GameVersion changes are intentional.
- Deploy to the actual Steam Mod root, and to the AppData development copy only when the deploy script maintains one; never overwrite unrelated user settings.
- Compare SHA256 of source output and every deployed DLL/PDB.
- Restart the game completely; confirm plugin load and absence of Harmony/Unity errors in `Player.log`.
- Exercise normal, disabled, repeated-refresh, scene-close, empty-data, invalid-request, and save/load cases.

## References

- [mod-architecture.md](references/mod-architecture.md): side selection and plugin layout.
- [templates-and-contracts.md](references/templates-and-contracts.md): entrypoints, Harmony targets, and DTO shapes.
- [harmony-communication.md](references/harmony-communication.md): patch and bridge safety rules.
- [config-packaging.md](references/config-packaging.md): settings, dependencies, and package hygiene.
- [current-build-and-deploy.md](references/current-build-and-deploy.md): current Roslyn worker commands and deployment verification.
- [update-compatible-debugging.md](references/update-compatible-debugging.md): game-update API/UI regression workflow.
- [validation-debugging.md](references/validation-debugging.md): final runtime and cleanup audit.
