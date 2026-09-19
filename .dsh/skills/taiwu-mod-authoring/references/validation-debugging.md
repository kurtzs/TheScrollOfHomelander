# Validation And Debugging

Use this reference before final verification, runtime debugging, or reviewing a broken Mod.

## Build Validation

Check:

- The frontend build resolved the frontend Managed root and the backend build resolved the backend root (the compiler picks the reference set by directory, so a wrong `--game-libs` silently changes the side).
- The shared source directory was compiled into both assemblies.
- `Config.lua` plugin paths match the actual build outputs.
- The compiler reported zero errors. Warnings (obsolete APIs, duplicate `using`) need review but do not fail a build; treat any error diagnostic as fatal.
- No game DLL, `bin`, `obj`, or decompiled export ended up in the deployed Mod directory.

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

## Local Workflow

1. Read the current sources and the target Mod's `Config.lua`; resolve the plugin paths and the actual deployment destinations.
2. Resolve the game API with `TaiwuStudio.DecompilerWorker` (`find-type` → `find-member` → decompile one member) and record side, assembly, type, member, and token.
3. Edit or add plugin source under the side's source directory, keeping shared types in the shared directory.
4. Build the side(s) with `TaiwuStudio.RoslynWorker` against the matching game library directory.
5. Deploy to the game `Mod` directory with the project's deploy script, verifying SHA256 for every copied file.
6. Ask the user to fully restart the game, then inspect `Player.log` for plugin load, Harmony target failures, and the feature's own log lines. This workspace never launches the game itself.
