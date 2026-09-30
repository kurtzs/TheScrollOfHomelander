---
name: the-scroll-of-homelander-mod
description: Work on the 太祖绘卷 / TheScrollOfHomelander Mod in this workspace — the real paths, Build-Deploy.ps1 pipeline, frontend/backend split, feature and setting layout, patch groups, user-data JSON stores, and release manifest. Use for any change inside mods/TheScrollOfHomelander, any build or deploy of this Mod, adding a feature or setting to it, or answering questions about how this specific Mod is structured.
---

# TheScrollOfHomelander Mod (太祖绘卷)

This skill is about *this* Mod in *this* workspace. For a real game API or patch target, load `taiwu-decompiled-api` first. For generic Taiwu Modding rules (side boundaries, Harmony policy, packaging hygiene), load `taiwu-mod-authoring`.

On a Linux host, first follow [taiwu-linux-development](../taiwu-linux-development/SKILL.md) for installation validation, direct workers, and target-runtime path discovery. `Build-Deploy.ps1` is Windows-only as currently implemented; changing `-GameRoot` is not sufficient. There is no full Linux release/deploy pipeline yet.

## Hard Facts

| Thing | Value |
| --- | --- |
| Mod root (source of truth) | `mods\TheScrollOfHomelander` |
| Plugin identity | `Title = 太祖绘卷(...)`, author `TenMountainMoon&ElysianSunaker`, `FileId = 3749026190` |
| Frontend plugin | `Plugins\Front\BetterTaiwuScrollFrontend.dll` (+ `.pdb`), assembly `BetterTaiwuScrollFrontend` |
| Backend plugin | `Plugins\Back\BetterTaiwuScrollBackend.dll` (+ `.pdb`), assembly `BetterTaiwuScrollBackend` |
| Source | `Scripts\Frontend`, `Scripts\Backend`, `Scripts\Shared` (Shared compiles into both) |
| Windows build / deploy | `.\mods\TheScrollOfHomelander\Build-Deploy.ps1` (compile + manifest), add `-Deploy` to publish |
| Windows pipeline release record | `mods\TheScrollOfHomelander\release-manifest.json` |
| Windows pipeline compiler records | `mods\TheScrollOfHomelander\build-records\Frontend.json`, `Backend.json` |
| Windows runtime user data | `%USERPROFILE%\Documents\TheScrollOfHomelander\*.json` (game-written; never put source there) |
| Steam Mod target | `<gameRoot>\Mod\TheScrollOfHomelander` |
| Windows development copy | `%LOCALAPPDATA%\TaiwuStudio\ModDevelopment\TheScrollOfHomelander-quality` |

Windows default `gameRoot` is `C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu`; table paths use Windows notation. For Windows/Proton targets, resolve runtime data in the actual target profile, not the Linux host's Documents directory. Other directories, such as a historical checkout under `Documents\GitHub\taiwu_studio`, are archives: do not edit or push them.

## Working Rules For This Mod

1. Read the current code before editing. Feature files are named `<Feature>Patches.cs` / `<Feature>Support.cs` under `Scripts\Frontend\Features` and `Scripts\Backend\Features`; shared state lives in `Scripts\Frontend\Shared`.
2. Resolve the game API against the installed DLLs, never from an old dump. Prefer the existing helper in the Mod (`MakeGameApi`, `ReflectionHelpers`, `ModUserDataPaths`) over a new reflection call.
3. Preserve vanilla behaviour whenever the feature's setting is off. Every user-visible change hangs off a `Config.lua` setting.
4. On Windows, build with `Build-Deploy.ps1`; on Linux, use the linked skill's direct Roslyn worker workflow. Treat any compile error as a stop. Do not introduce a Mod-local `.csproj` or mix side-specific game references.
5. Never launch the game or run in-game tests. Deployment is followed by a full manual restart, which the user performs.
6. Keep edits to unrelated features and unrelated Config groups out of a change. Do not reformat untouched files.

## Feature And Setting Layout

- `Config.lua` owns user-facing settings: `DefaultSettings[]` entries with `Key`, `SettingType` (`Toggle` / `Slider`), `DefaultValue`, `DisplayName`, `Description`, `GroupName`, plus the `SettingGroups` list. Add one entry per new toggle and reuse an existing group unless the feature genuinely needs a new one.
- `Plugin.cs` mirrors every key into a `static` field and reads it in `LoadSettings` / `LoadIntSetting` / `LoadSettingDefault`. Never use `ModManager.GetSetting` directly elsewhere.
- `OnModSettingUpdate` must re-read settings and refresh anything already on screen. `Dispose` must undo every install: unpatch, restore vanilla visuals, clear statics, and reset controllers.
- Group switches exist and matter: `enable_advance_month_optimization` (perf) vs `enable_game_performance_optimization` (in-game), and `auto_harvest_after_advance_month` gates the harvest/recruit children. Keep month/turn optimizations separate from in-game performance optimizations, as the current groups do.
- Per-feature JSON stores (for example `MemoryOptimizationSettings`, `PurchaseOptimizationSettings`, `AutoChickenCareSettings`, `AutoCultivationSettings`) are separate from `Config.lua` settings: `Config.lua` is the toggle, the JSON store keeps structural memory (last filter, last tool, thresholds).

## Patching Model

`Scripts\Shared\ModPatchGroups.cs` installs patch classes in independent groups and rolls a group back if installation throws. Group keys come from `ModPatchGroups.Classify(type)`, which matches on the class name: `ModSession*` → `session`, `ItemScroll*` → `item-scroll`, anything containing `AdvanceMonth` → `month`, `Recruit`/`Harvest`/`BuildingEarnings` → `recruit`, `Make`/`Repair` → `make`, `Map`/`WorldState` → `map`, `Exchange`/`Shop`/`Purchase`/`Transfer` → `trade`, `Cultivation` → `cultivation`, `Search` → `search`, otherwise `ui`.

Consequences when adding a patch class:

- The class name decides its group, so name a new patch file so that `Classify` lands it in the intended group. If a name would be misclassified, extend `Classify` deliberately instead of relying on luck.
- The frontend additionally excludes `ItemListRefreshCoordinator*` from groups and patches those classes through `ItemListRefreshCoordinatorPatchLifecycle`, and validates the `make` group through `MakeGameApi.Validate()`.
- Patch classes are discovered by `[HarmonyPatch]` on the type; a class without it is never installed.

## Frontend / Backend Bridge

- Backend registers handlers in `Plugin.Initialize` through `DomainManager.Mod.AddModMethod(ModIdStr, <name>, <handler>)`; `Scripts\Shared\ModProtocol.cs` holds the shared name constants and `Version = 1`.
- Requests carry `version` (must equal `ModProtocol.Version`), target ids, and every switch that changes behaviour; responses always carry `success`, `code`, `message`, and structured fields. See `Scripts\Backend\Features\AutoRepairService.cs` for the reference implementation.
- The backend is authoritative: re-fetch objects, validate ownership, source, quantities and preconditions, and use domain APIs for mutation. The frontend handles failure, panel closure, and stale responses.
- Put frontend intent in the frontend and authority in the backend. Do not add a backend reference to `Assembly-CSharp` or a frontend mutation of authoritative state.

## Build And Deploy

Windows PowerShell pipeline:

```powershell
# compile both sides, rewrite release-manifest.json, no deployment
.\mods\TheScrollOfHomelander\Build-Deploy.ps1

# compile, then back up the Steam Mod and publish to Steam + the development copy
.\mods\TheScrollOfHomelander\Build-Deploy.ps1 -Deploy
```

The script compiles Frontend against `The Scroll of Taiwu_Data\Managed` and Backend against `Backend`, each including `Scripts/Shared`, aborts on the first failing side, then writes `build-records\<Side>.json` and `release-manifest.json` (source digest, per-file source hashes, game assembly fingerprints, DLL/PDB hashes). With `-Deploy` it backs up the Steam Mod to the Windows `Documents\TaiwuModBackups\<timestamp>-quality-<id>`, copies sources normally and publishes each plugin DLL/PDB individually via a temporary file and replace/move, then verifies plugin and source SHA256 hashes. Only individual plugin publication is atomic, not source copying or the whole deployment; hash mismatches fail the run.

After a deploy, tell the user to fully exit and restart the game: a running Unity process keeps the old assembly loaded. When a change touches `Config.lua`, state explicitly which keys and version values changed.

## Definition Of Done

- Both sides compile with zero errors through `Build-Deploy.ps1` on Windows or the linked direct worker workflow on Linux; a worker-only smoke test is not a Mod build or release verification.
- `Config.lua` declares exactly the built plugin paths, and any new setting key exists in both `Config.lua` and the matching `Plugin.cs` field.
- Deployed `Plugins\**\*.dll`/`.pdb` hashes match the build output (the script checks this when `-Deploy` is used).
- The patch group of a new patch class is the intended one, and installation failure of that group degrades only that feature.
- Settings changes take effect without a restart; the disabled path restores vanilla behaviour.
- The user is told to restart the game and what to look for in `Player.log` (`[BetterTaiwuScroll]` lines, `Installed patch group:`, `Disabled patch group`).

## References

- [mod-map.md](references/mod-map.md): file-by-file map of the Mod, key types, and where a change belongs.
- [build-and-deploy.md](references/build-and-deploy.md): the pipeline in detail, with manifest fields and failure modes.
- [settings-and-userdata.md](references/settings-and-userdata.md): adding a setting, the JSON stores, and the user-data directory.
- [feature-checklist.md](references/feature-checklist.md): step-by-step recipe for adding or changing a feature.
