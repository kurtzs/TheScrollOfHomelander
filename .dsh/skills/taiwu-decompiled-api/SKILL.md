---
name: taiwu-decompiled-api
description: Inspect the installed The Scroll of Taiwu frontend and backend assemblies for real types, members, source spans, fields, call order, and Harmony targets, using only tools/TaiwuStudio.DecompilerWorker. Use when a Mod needs an exact patch target, an update-compatibility check, a frontend/backend side decision, overload or token evidence, or the cause of a runtime error.
---

# Taiwu Decompiled API

Decompilation is evidence about the game build that is installed right now. Never reuse an old source dump as ground truth, and never copy decompiled code into Mod sources — read the narrow span you need and record enough identity to reproduce the lookup.

## Choose The Source

| Side | Assembly directory | What lives there |
| --- | --- | --- |
| Frontend | `<gameRoot>\The Scroll of Taiwu_Data\Managed` | `Assembly-CSharp.dll` (Unity views, controllers, generated UI wrappers), `TaiwuModdingLib.dll`, `0Harmony.dll` |
| Backend | `<gameRoot>\Backend` | `GameData.dll`, `GameData.Shared.dll`, `TaiwuModdingLib.dll`, `0Harmony.dll` |

Default `gameRoot`: `C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu`. Verify it before relying on it; pass `-GameRoot` when the library moved.

`GameData.Shared.dll` exists on both sides with different content. Read the copy from the directory of the side you are compiling for, and never compile a backend plugin against `Managed`.

## The Only Tool: TaiwuStudio.DecompilerWorker

`tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe` (.NET 8, source in `tools\TaiwuStudio.DecompilerWorker\Program.cs`). It reads installed DLLs directly: no index, no database, no cache to refresh, no external application to open.

Every command prints one JSON envelope:

```json
{ "ok": true, "payload": { }, "diagnostics": [ ] }
```

`ok:false` means failure and `payload` is null; the reason is in `diagnostics[].message`. Exit code is 0 on success, 2 on a handled failure, 1 on an exception.

| Command | Required | Optional | Returns |
| --- | --- | --- | --- |
| `scan` | `--managed-dir` | — | metadata for the core assemblies in that directory |
| `find-type` | `--managed-dir --query` | `--assembly --limit` | `results[]` with `assembly`, `fullName`, `token`; plus `totalMatches`, `limit` |
| `find-member` | `--managed-dir --query` | `--assembly --limit` | `results[]` with `typeFullName`, `kind`, `name`, `signature`, `token` |
| `decompile-type` | `--managed-dir --assembly --type` | — | `source` (whole type) plus `sourceSpan` |
| `decompile-member` | `--managed-dir --assembly` and (`--token` or `--type --member`) | — | `memberName`, `token`, `source` for the **declaring type**, plus `sourceSpan` for the member |
| `export-core` / `export-dir` | `--managed-dir --out-dir` | — | writes `sources\...` and `summary.json` for offline grep |

Notes that save time:

- `--assembly` takes an assembly id (`GameData`), a file name (`GameData.dll`), or a full path. Without it, every assembly in `--managed-dir` is searched — slower, but it finds a member whose declaring assembly you do not know.
- `decompile-member` decompiles the **whole declaring type** and only *locates* the member. A large domain type can exceed 30k lines, so read `payload.sourceSpan` and slice the source; never print it whole.
- `sourceSpan` is the declaration line plus the following 80 lines. It can include neighbouring members, and for compiler-generated names it is approximate — confirm the signature in the text.
- Property accessors, operators, and event accessors are compiled names: `get_X`, `set_X`, `op_Equality`, `add_X`. `find-member X` finds them; `decompile-member --member X` does not.
- Constructors are `.ctor`, static constructors `.cctor`. Nested types carry `+` in `fullName` (`Outer+Inner`); pass names back exactly as the worker returned them.
- `--limit` defaults to 50 and caps the returned array, not `totalMatches`. Raise it when results were truncated.

## Query Wrapper

`scripts/query.ps1` is a readable front end for the worker. It locates the workspace root and the worker (Release, then Debug), resolves which game directory holds the requested assembly, and prints only the member's span with context instead of the whole type. `-Json` returns the raw worker envelope for scripting; `-Full` prints the entire type.

```powershell
$q = "<skillRoot>\scripts\query.ps1"
& $q find-type   CharacterDomain -Assembly GameData -Limit 20
& $q find-member GetTaiwuCharId  -Assembly GameData -Limit 20
& $q member      GameData.Domains.Taiwu.TaiwuDomain GetTaiwuCharId -Assembly GameData
& $q member      -Token 0x0600589E -Assembly GameData
& $q type        GameData.Domains.Taiwu.TaiwuDomain -Assembly GameData -Context 5
& $q find-type   SortAndFilter -Assembly Assembly-CSharp
& $q member      Game.Components.SortAndFilter.SortAndFilter ApplyFilterLineStates -Assembly Assembly-CSharp -Json
```

Expected cost: `find-*` is under a second; decompiling a large domain type takes seconds to tens of seconds. Batch your questions instead of re-decompiling the same type. Set `$env:TAIWU_DECOMPILER_WORKER` if the worker lives elsewhere.

## Query Workflow

1. Search the exact type or member name (`find-type`, then a narrower `find-member`).
2. Narrow by assembly and side. Confirm declaring type, namespace, and assembly file name.
3. Decompile that one member (or type) and read a small span, plus callers when call order matters.
4. Confirm visibility, static/instance status, generic arity, parameter types, return type, and metadata token.
5. Only then choose a Harmony patch, and check whether a safer lifecycle or domain method exists.

## Evidence Record

Keep this record in your notes and in the change description. A patch target that cannot be written this way is not yet verified:

```text
side:     frontend | backend
assembly: exact DLL file name
type:     full namespace-qualified type, with generic arity and + for nested
member:   exact name and overload parameter list
token:    0x0600.... when available
patch:    Prefix | Postfix | Transpiler | Finalizer
reason:   why this point is stable and necessary
```

## Update And Error Investigation

When a method disappears after a game update, compare the current type's public methods, private fields, call order, and callback sequence instead of trusting memory. An old dump can make an invalid target compile while Harmony fails at load time with `Undefined target method`.

- `InvalidCastException`: verify the actual field type before using `Traverse` or a field helper.
- UI `NullReferenceException` or a dead coroutine: inspect `Awake`, `OnEnable`, `OnDisable`, the rebuild path, and async completion together.
- List/filter bugs: inspect both the view (`GetStateFromUI`, `ApplyFilterLineStates`) and its controller (`GenerateFilter`, `SetItemList`, `RefreshList`, save/restore). The visible selection and the controller's effective selection are often stored separately, so exercise the whole refresh sequence.
- Save/load or month-turn bugs: read the domain save/commit path and its dependencies before patching a single method.

## Output Discipline

Report the exact type, member, assembly, token, and the source span you read. Treat decompiled code as guidance: iterator/state-machine names and formatting differ from the original source, and generated wrappers hide the real call. Do not paste large decompiled files into Mod code, notes, or answers, and do not assume a frontend type is available to the backend.

## References

- [core-assemblies.md](references/core-assemblies.md): what each game assembly contains and which one to search first.
- [exports.md](references/exports.md): using `export-core`/`export-dir` output as an offline, greppable snapshot.
- [common-entrypoints.md](references/common-entrypoints.md): search terms and patch-selection guidance.
