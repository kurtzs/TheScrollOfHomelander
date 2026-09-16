---
name: taiwu-decompiled-api
description: Inspect the currently installed The Scroll of Taiwu frontend and backend assemblies for real types, members, source spans, call order, fields, and Harmony targets. Use when a Mod needs an exact patch target, an update-compatible API check, a frontend/backend side decision, or evidence for a runtime error.
---

# Taiwu Decompiled API

Use decompilation as evidence for the installed game, not as a source-copying shortcut. Keep the loaded context narrow and record enough identity to reproduce the lookup.

## Choose The Source

- Frontend UI, Unity, views, input, and display code: `<gameRoot>/The Scroll of Taiwu_Data/Managed`.
- Backend rules, domains, `DataContext`, and `GameData.*`: `<gameRoot>/Backend`.
- Use the current DLL for a game-update decision. Use the Taiwu Studio cache for fast navigation only after checking its DLL fingerprint/version.
- Never compile a backend plugin against frontend Managed assemblies or vice versa.

## Query Workflow

1. Search the exact type or member name.
2. Narrow by assembly, side, namespace, and overload.
3. Inspect the declaring type and a small source span around the target and its callers/callbacks.
4. Confirm visibility, static/instance status, generic arity, parameter types, return type, and metadata token.
5. Choose a Harmony patch only after checking whether a safer lifecycle/domain method exists.

Use the cache scripts when the index is current. Set `$skillRoot` to this skill's directory:

```powershell
$skillRoot = "C:\Users\Administrator\.codex\skills\taiwu-decompiled-api"
py -3 $skillRoot\scripts\find_type.py CharacterDomain
py -3 $skillRoot\scripts\find_member.py GetCharacter
py -3 $skillRoot\scripts\show_source.py GameData.Domains.Character.CharacterDomainHelper --context 80
py -3 $skillRoot\scripts\find_patch_targets.py SetHealth
```

The scripts can use `TAIWU_STUDIO_INDEX` and `TAIWU_DECOMPILED_CACHE`. If they return no result or the game just updated, refresh the index/cache or use the current worker directly.

## Current Decompiler Worker

Taiwu Studio's `TaiwuStudio.DecompilerWorker` reads the installed DLL without requiring a Mod project. Its JSON response includes assembly, full type name, source, source span, and diagnostics.

```powershell
$worker = "tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe"
$managed = "<gameRoot>\The Scroll of Taiwu_Data\Managed"

& $worker --command find-type --managed-dir $managed --assembly Assembly-CSharp --query "SortAndFilter" --limit 50
& $worker --command find-member --managed-dir $managed --assembly Assembly-CSharp --query "OnInit" --limit 50
& $worker --command decompile-type --managed-dir $managed --assembly Assembly-CSharp --type "Game.Components.SortAndFilter.SortAndFilter"
& $worker --command decompile-member --managed-dir $managed --assembly Assembly-CSharp --type "Game.Components.SortAndFilter.SortAndFilter" --member "ApplyFilterLineStates"
```

For backend queries, replace `$managed` with `<gameRoot>\Backend` and select the actual backend assembly. Use `--token 0x0600....` when overload names are ambiguous. Parse the JSON and extract only the method span; avoid dumping an entire type into the conversation.

## Harmony Target Record

Before writing a patch, record:

```text
side: frontend | backend
assembly: exact DLL name
type: full namespace-qualified type, including generic arity
member: exact name and overload parameter list
token: metadata token when available
patch: Prefix | Postfix | Transpiler | Finalizer
reason: why this lifecycle/domain point is stable and necessary
```

Prefer Postfix for observation/additive behavior, a non-skipping Prefix for argument preparation, and Transpiler only when no method-level hook can express the behavior. For constructors use `MethodType.Constructor`; for static constructors use `MethodType.StaticConstructor`.

## Update And Error Investigation

When a method disappears, compare the current type's public methods, private fields, call order, and callback sequence. An old cache can make an apparently valid target compile while Harmony fails at runtime. For `InvalidCastException`, verify the actual field type before using `Traverse` or a field helper. For UI `NullReferenceException`/inactive coroutine errors, inspect `Awake`, `OnEnable`, `OnDisable`, rebuild, and async completion paths together.

For sort/filter or list bugs, inspect both the view (`GetStateFromUI`, `ApplyFilterLineStates`) and its controller (`GenerateFilter`, `SetItemList`, `RefreshList`, save/restore methods). The visible selection and the controller's effective selection may be stored separately; test the full refresh sequence rather than only the button state.

## Output Discipline

Report exact type/member/assembly evidence and the source span used. Treat decompiled code as guidance: iterator/state-machine names and formatting may differ from original source. Do not paste large decompiled files into Mod code or answers, and do not assume a frontend type is available to the backend.

## References

- [core-assemblies.md](references/core-assemblies.md): assembly-side boundaries.
- [cache-layout.md](references/cache-layout.md): cache/index discovery when scripts fail.
- [common-entrypoints.md](references/common-entrypoints.md): where to start broad searches.
