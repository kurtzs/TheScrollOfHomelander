# Common Entrypoints

Search terms to start from, then narrow to an exact type and member with `find-type` / `find-member`.

| Area | Try searching |
| --- | --- |
| Domains | `Domain`, `CharacterDomain`, `CombatDomain`, `ItemDomain`, `MapDomain`, `OrganizationDomain`, `BuildingDomain`, `TaiwuDomain` |
| Data helpers | `DomainHelper`, `DataIds`, `MethodIds`, `ItemTemplateHelper`, `Config` |
| Mod entry | `TaiwuRemakePlugin`, `PluginConfig`, `ModManager`, `ModIdStr`, `OnModSettingUpdate`, `AddModMethod` |
| Views and components | `View`, `UI_`, `Component`, `Panel`, `Window`, `Scroll`, `Tooltip`, `Toggle` |
| Events | `GEvent`, `EEvents`, `Event`, `Dispatch`, `Notification`, `Broadcast` |
| Patch-sensitive | domain mutations and setters, save/load paths, month-turn planning, combat calculation, UI refresh/rebuild |

## Pick The Right Patch Point

- Data or rule changes: patch domain/data APIs on the backend, not UI methods.
- Presentation-only changes: patch the view's refresh/rebuild path on the frontend, and restore vanilla state when the setting is off.
- Frontend intent that changes authoritative state: do not patch deep; send a `DomainManager.Mod.AddModMethod` request and let the backend validate and apply it.
- Prefer a stable lifecycle method (`Awake`, `OnInit`, `OnEnable`, `Refresh`) over a per-frame hook, and `Postfix` over a skipping `Prefix`.
- Reach for a `Transpiler` only when no method-level hook can express the behaviour, and never mutate a collection while vanilla code enumerates it.

## Before Writing The Patch

Confirm, in this order: exact overload (parameter array or metadata token), `static` vs instance, return type, and whether the method runs on the expected side. Then check whether a safer domain or lifecycle method already does the job — a decompiled method that merely *looks* convenient is often not the stable entry point.
