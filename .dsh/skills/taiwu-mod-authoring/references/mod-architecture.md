# Mod Architecture

Use this reference when creating a Mod project, splitting frontend/backend code, or editing `Config.lua`.

## Side Model

Taiwu Mods commonly have two runtime sides:

| Side | API root (Windows examples) | Responsibilities |
| --- | --- | --- |
| Frontend | `C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\The Scroll of Taiwu_Data\Managed` | Unity UI, input, sprites, textures, scene objects, display state, `ModManager`, `UIBase`, `AsyncMethodDispatcher` |
| Backend | `C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\Backend` | Authoritative game data and rules, `GameData.*`, `GameData.Domains.*`, `DomainManager`, `DataContext`, `SerializableModData` |

Choose frontend for visual/UI behavior. Choose backend for persistent state, validation, character/item/world data, combat rules, action rules, and deterministic simulation behavior. Choose both when frontend UI collects intent and backend applies the rule.

## Recommended Project Layout

```text
MyMod/
  Config.lua
  Settings.Lua
  Plugins/
    Front/MyModFrontend.dll
    Back/MyModBackend.dll
  Scripts/
    Frontend/
    Backend/
    Shared/
  Assets/
  TianDao/
```

Valid plugin paths may be nested:

```lua
FrontendPlugins = {
    [1] = "Front/MyFrontend.dll",
}
BackendPlugins = {
    [1] = "Back/MyBackend.dll",
}
```

Use separate frontend/backend projects when references differ. Keep a small shared assembly only for stable constants, DTOs, or codecs. Shared code must avoid Unity types and backend-only `GameData` state types unless it is compiled separately per side.

## Minimal Config.lua

```lua
return {
    Title = "My Mod",
    Author = "Author",
    Version = "1.0.0.0",
    GameVersion = "1.0.7.0",
    Source = 1,
    FrontendPlugins = {
        [1] = "MyModFrontend.dll",
    },
    BackendPlugins = {
        [1] = "MyModBackend.dll",
    },
    TagList = {
        [1] = "Plugin",
    },
}
```

Add dependencies for framework Mods or SDKs:

```lua
Dependencies = {
    [1] = 3737804824,
}
```

Recognize legacy declarations when maintaining older packages:

```lua
FrontendPluginsLegacy = {
    [1] = "../LegacyPlugins/MyFrontend.dll",
}
BackendPluginsLegacy = {
    [1] = "../LegacyPlugins/MyBackend.dll",
}
```

## Entry Class

```csharp
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

[PluginConfig("MyModBackend", "Author", "1.0.0")]
public sealed class MyModBackend : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    public override void Initialize()
    {
        _harmony = Harmony.CreateAndPatchAll(typeof(MyModBackend));
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
```

Common lifecycle methods:

- `Initialize`: register backend methods, read settings, apply Harmony patches, start watchers, load assets.
- `Dispose`: unpatch, unregister, dispose watchers/subscriptions, cancel loops.
- `OnModSettingUpdate`: reload settings.
- `OnEnterNewWorld` / `OnLoadedArchiveData`: refresh runtime state after world/archive changes.

## Architecture Patterns

- Frontend-only visual Mod: one frontend plugin, asset folders, UI/renderer patches, no backend mutation.
- Backend-only gameplay Mod: one backend plugin, domain Harmony patches or backend methods, optional `Settings.Lua`.
- Split UI/action Mod: frontend UI and request contract, backend `AddModMethod` or domain method target.
- SDK/config platform: frontend schema UI, backend action/config service, shared abstractions, dependency declarations.
- Runtime tool: frontend/backend file bridge, script execution, watchers, async queues, strict cleanup.

## Design Checks

- Is every declared DLL actually built to the declared path?
- Are frontend and backend references separated?
- Does a backend method validate the current save state before mutation?
- Does frontend UI code handle scene reloads and duplicated controls?
- Does `Dispose` undo everything created in `Initialize`?
- Are build scratch folders and decompiled exports excluded from deployed output?
