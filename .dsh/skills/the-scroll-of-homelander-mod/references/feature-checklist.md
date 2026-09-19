# Feature Checklist

Use this for any new feature or behaviour change in this Mod. Skip a step only if you can say why it does not apply.

## 1. Locate The Behaviour

1. Name the user-visible outcome and the screen it happens on.
2. Decide the side: Unity/UI/input/assets → frontend; authoritative state, save, month turn, item or character rules → backend; "user asked, game must validate" → both with a bridge call.
3. Load `taiwu-decompiled-api` and find the real method: `find-type`, then `find-member`, then decompile just that member. Record side, assembly, full type, member/overload, and token.

## 2. Declare The Setting

1. Add the key to `Config.lua` `DefaultSettings` in the group that owns the feature (see `settings-and-userdata.md`).
2. Mirror it into the side's `Plugin.cs`: a `static` field plus a `LoadSetting`/`LoadSettingDefault`/`LoadIntSetting` line, with a clamp for sliders.
3. If the feature belongs to a gated family, respect the existing group switch instead of adding a parallel one.

## 3. Implement The Patch

- One concern per patch class; put it in the right folder (`Scripts\Frontend\Features`, `Scripts\Backend`, `Scripts\Backend\Recovered` for month-turn work).
- Name the class so `ModPatchGroups.Classify` puts it in the intended group (see the table in `SKILL.md`). Extend `Classify` deliberately if no name fits.
- Prefer `Postfix` for additive behaviour, a non-skipping `Prefix` for argument preparation, `Finalizer` only for deliberate containment. Disambiguate overloads with the parameter array or token; use `MethodType.Constructor` / `StaticConstructor` for constructors.
- Make the patch idempotent and guard the off path: when the setting is false, the game must behave exactly as vanilla — including restoring any visual or state change already applied.
- Guard Unity objects (`== null`, destroyed, inactive) and defer layout work until the owner is active; coalesce same-frame refreshes through `UiLayoutRefreshQueue` rather than refreshing inside `Update`.
- Cache `FieldInfo`/`MethodInfo`/component lookups per type or instance; use `ReflectionHelpers` when a private member is unavoidable.
- Keep per-view state keyed by a stable identity (building, item, store key), never in one global static.

## 4. Bridge If Needed

- Add the method name constant to `Scripts\Shared\ModProtocol.cs` and register the handler in `Scripts\Backend\Plugin.cs` via `DomainManager.Mod.AddModMethod`.
- Request: `version` (must equal `ModProtocol.Version`), `requestId` when the call can outlive the panel, explicit target ids, and every switch that changes the outcome.
- Response: `success`, `code`, `message`, plus structured fields. Model it on `AutoRepairService`.
- Backend re-fetches and validates before mutating; frontend handles failure, panel closure, and stale responses, and never retries a submitted mutation on timeout.

## 5. Register Lifecycle

- Anything created at runtime (coroutines, controllers, Harmony instances outside patch groups, component references) must be reset or disposed in the plugin's `Dispose`, and reset on session change (`ModSession.Resetting`).
- Settings-store loads go in `Initialize`; live re-application goes in `OnModSettingUpdate`.

## 6. Build

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1
```

Both sides must report zero errors. Read `build-records\<Side>.json` when a warning needs the exact file/line. Fix real errors; do not silence them with `#pragma warning disable` without a reason.

## 7. Deploy And Hand Over

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1 -Deploy
```

Then state, explicitly:

- which files changed and which setting keys are new or changed;
- whether `Config.lua` metadata (for example `Version`) was touched;
- that the user must fully exit and restart the game, because the loaded assembly is retained;
- what to check in `Player.log`: `[BetterTaiwuScroll]` lines, `Installed patch group: <group>`, and the absence of `Disabled patch group`.

## 8. Report

Include the API evidence (side / assembly / type / member / token), the setting key, the build result, and the deployment hash verification result. Do not claim runtime success — this workspace never launches the game; in-game acceptance is the user's step.
