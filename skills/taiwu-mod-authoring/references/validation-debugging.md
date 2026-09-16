# Validation And Debugging

Use this reference before final verification, runtime debugging, or reviewing a broken Mod.

## Build Validation

Check:

- Frontend project references frontend Managed root.
- Backend project references backend root.
- `Config.lua` paths match build outputs.
- Game-provided assemblies use `Private=false`.
- Third-party dependencies are present only when needed.
- Warnings about missing declared DLLs are acceptable before first build, but errors after build indicate packaging mismatch.

## Runtime Validation

For frontend Mods:

- Confirm UI patch runs after the target UI object exists.
- Confirm controls are not duplicated after refresh/reopen.
- Confirm Unity objects are null-guarded.
- Confirm assets load from Mod-relative paths.

For backend Mods:

- Confirm backend plugin loads without frontend assemblies.
- Confirm `AddModMethod` registration happens once.
- Confirm request validation rejects stale ids or illegal actions.
- Confirm save/load/world-enter lifecycle refreshes cached state.

For split Mods:

- Confirm frontend and backend agree on method name or method id.
- Confirm request/response keys match exactly.
- Confirm frontend handles backend failure response.
- Confirm backend logs enough detail for failed validation.

## Harmony Debugging

If a patch does not run:

- Re-check side and assembly.
- Re-check overload parameters and method token.
- Confirm `Harmony.CreateAndPatchAll` scans the type that contains the patch.
- Confirm nested/private patch classes are discoverable by the chosen Harmony version.
- Confirm constructor/static constructor targets use `MethodType`.

If a patch breaks behavior:

- Change skipping Prefix to non-skipping Prefix or Postfix where possible.
- Preserve original behavior on invalid input.
- Avoid mutating collections while vanilla code enumerates them.
- Avoid per-frame heavy work in UI or simulation methods.

## Cleanup Audit

Search for:

- `new Harmony`, `CreateAndPatchAll`, `PatchAll`.
- `FileSystemWatcher`.
- `CancellationTokenSource`.
- `Task.Run`, channels, queues, coroutines.
- config source registration and event subscription APIs.
- temporary Unity `GameObject` creation.

Each should have a matching cleanup path in `Dispose` or equivalent lifecycle teardown.

## Taiwu Studio Workflow

Recommended local workflow:

1. Create/open Mod project.
2. Rebuild API index for both frontend and backend.
3. Search/decompile target APIs.
4. Generate or edit plugin files.
5. Build with side-specific references.
6. Deploy to game `Mod` directory.
7. Launch game and inspect plugin load/runtime logs.

For Taiwu Studio repo development, do not write `docs\COMMIT_CACHE.md`; create local git commits for completed repo changes.
