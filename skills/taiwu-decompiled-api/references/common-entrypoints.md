# Common Entrypoints

Use these search terms as starting points, then narrow by exact type/member:

- Domain systems: `Domain`, `CharacterDomain`, `CombatDomain`, `LifeRecord`, `ItemDomain`, `MapDomain`, `OrganizationDomain`.
- GameData helpers: `DomainHelper`, `DataIds`, `MethodIds`, `Config`, `Data`, `Shared`.
- ModdingLib: `TaiwuRemakePlugin`, `Plugin`, `Config`, `Settings`, `ModId`, `OnModSettingUpdate`.
- UI: `UI_`, `Window`, `Panel`, `View`, `ContextMenu`, `Element`, `Item`.
- Events/messages: `Event`, `Message`, `Notification`, `Broadcast`, `On`, `Dispatch`.
- Harmony-sensitive methods: domain mutations, data setters, save/load paths, combat calculation paths, and UI refresh methods.

Patch selection guidance:

- For data changes, prefer domain/data APIs over UI methods.
- For presentation-only changes, patch UI refresh/render/update methods.
- For compatibility, avoid broad Transpilers until a Prefix/Postfix cannot express the change.
- Always inspect nearby decompiled source and overloads before generating a patch template.
