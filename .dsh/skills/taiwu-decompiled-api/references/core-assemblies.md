# Core Assemblies

Search the assembly for the side you are targeting. The same logical type can appear in more than one assembly with different content, so an assembly name is part of the evidence, not a detail.

## Frontend: `<gameRoot>\The Scroll of Taiwu_Data\Managed`

| Assembly | Contents |
| --- | --- |
| `Assembly-CSharp.dll` | Game-side compiled Unity code: views, controllers, UI components (`Game.Views.*`, `Game.Components.*`), input handling, generated wrappers, and runtime behaviour |
| `GameData.Shared.dll` | Shared data contracts and DTOs as the frontend sees them |
| `TaiwuModdingLib.dll` | Official Mod entry contracts: `TaiwuRemakePlugin`, `PluginConfig`, `ModManager`, settings and lifecycle hooks |
| `0Harmony.dll` | Harmony patch API (`Harmony`, `HarmonyPatch`, `HarmonyPatchType`) |
| `UnityEngine.*.dll` | Unity modules. Read them to confirm a Unity API signature, never to patch them |

## Backend: `<gameRoot>\Backend`

| Assembly | Contents |
| --- | --- |
| `GameData.dll` | Authoritative domains (`GameData.Domains.*`), domain helpers, save/load, domain methods, config data models |
| `GameData.Shared.dll` | Shared contracts used by both sides; the backend copy is the authoritative one |
| `TaiwuModdingLib.dll` | Same Mod contracts as the frontend copy |
| `0Harmony.dll` | Harmony patch API |

## Search Strategy

1. `find-type` first when you only know a concept (`CharacterDomain`, `SortAndFilter`, `Exchange`).
2. `find-member` when you know the member name; its `signature` column tells you the declaring type and parameter list.
3. `decompile-member` on the single target, then read the span — not the whole type.
4. Start with `GameData.*` for stable data-model and domain names, `Assembly-CSharp` for actual UI and runtime behaviour, `TaiwuModdingLib` only for plugin entry and lifecycle APIs, and `0Harmony` only to confirm patch API signatures.

Side boundaries are not negotiable: a backend plugin cannot reference `Assembly-CSharp.dll`, and frontend code cannot mutate authoritative state directly — it must go through `DomainManager.Mod.AddModMethod`.
