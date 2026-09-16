# Config And Packaging

Use this reference for settings, TianDao-style JSON config, dependencies, bundled DLLs, and deploy checks.

## Settings.Lua

Use `Settings.Lua` for simple user settings. Read values from the correct side:

- Frontend: `ModManager.GetSetting(ModIdStr, key, ref value)`.
- Backend: `DomainManager.Mod.GetSetting(ModIdStr, key, ref value)`.

Call the same load function from `Initialize` and `OnModSettingUpdate`.

## Attribute-Driven Settings

Some helper Mods map settings to fields through custom attributes. When extending this pattern:

- Reflect only fields carrying the setting attribute.
- Support bool, int, string, and custom `SerializableModData` separately.
- Keep frontend and backend setting reads side-correct.
- Avoid hiding required setting keys in magic strings without constants.

## TianDao-Style JSON Config

Common layout:

```text
TianDao/
  schema.json
  values.json
  values.export.json
  CN.json
  EN.json
```

Typical architecture:

- Frontend registers a schema source, often through a frontend registry service.
- Backend registers or opens the same source through backend SDK services.
- Backend subscribes to config changes and updates runtime flags.
- Harmony patches read runtime flags rather than parsing JSON every call.

Remember to unregister sources and dispose subscriptions in `Dispose`.

## Dependencies

Declare Mod dependencies in `Config.lua`:

```lua
Dependencies = {
    [1] = 3737804824,
}
```

Use dependencies for framework Mods such as config UI shells. If the dependency ships SDK DLLs, reference them at build time and decide whether they must also be packaged.

## Bundled DLLs

Bundle only dependencies the game does not provide. Do not ship large game assemblies.

If dependencies live in subdirectories, register assembly resolution early:

```csharp
AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
{
    var name = new AssemblyName(args.Name).Name;
    var candidate = Path.Combine(libDir, name + ".dll");
    return File.Exists(candidate) ? Assembly.Load(File.ReadAllBytes(candidate)) : null;
};
```

Backend .NET loading may require `AssemblyLoadContext.Resolving` instead of `AppDomain.AssemblyResolve`. Confirm with the side's runtime and existing reference Mods.

## Packaging Checklist

- `Config.lua` paths match actual output paths.
- Frontend plugin DLL is not declared as backend and backend DLL is not declared as frontend.
- `Dependencies` include required framework Mods.
- `Settings.Lua`, `TianDao`, JSON config, assets, and localization files are included.
- Third-party DLLs are included only when required.
- `.taiwu-studio`, obj/bin scratch folders, source maps, and decompiled caches are not deployed.
- Version and GameVersion are updated intentionally.

## Runtime Cleanup

Dispose all:

- Harmony instances.
- `FileSystemWatcher`.
- `CancellationTokenSource`.
- Channels/background loops.
- UI subscriptions and config subscriptions.
- Registered schema/config sources.
- Temporary Unity GameObjects created by the Mod.

## Packaging Review Questions

- Can a clean game install load the Mod without local absolute paths?
- Are declared dependencies visible to the game loader?
- Does the package avoid copying game DLLs?
- Is there a plan for save compatibility if backend data shape changes?
