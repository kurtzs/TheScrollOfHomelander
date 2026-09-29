# API And Tooling

Use this reference before searching APIs, selecting patch targets, or decompiling game code.

The PowerShell examples below are Windows-specific. On Linux, follow [taiwu-linux-development](../../taiwu-linux-development/SKILL.md) to validate the installation and use `bash tools/run-worker.sh decompiler ...` directly. `query.ps1` is Windows-only as currently implemented, not made portable by changing `-GameRoot` alone.

## Search The Installed Assemblies

Resolve the game root first, then search by exact type or member name with `TaiwuStudio.DecompilerWorker`. Narrow early:

- Side: frontend (`The Scroll of Taiwu_Data\Managed`) for Unity/UI APIs; backend (`Backend`) for `GameData` and domain APIs.
- Assembly: `Assembly-CSharp`, `TaiwuModdingLib`, `0Harmony`, `GameData`, or a specific `GameData.*`.
- Kind: the worker distinguishes `method`, `field`, and `property` in its results.
- Namespace: `GameData.Domains`, `GameData.Domains.Character`, frontend UI namespaces, and so on.

The `taiwu-decompiled-api` skill documents the commands and provides `scripts\query.ps1`, which prints one member's source span instead of the whole declaring type.

Recommended flow:

1. Search a likely type name.
2. Filter to the intended side.
3. Filter to a symbol kind.
4. Inspect the selected type/member.
5. Decompile a narrow span.
6. Check overload parameters and method token before generating a Harmony target.

## taiwu-decompiled-api Skill

Use `taiwu-decompiled-api` for real type/member/source details. It owns the full command reference and ships `scripts\query.ps1`, a readable wrapper that prints one member's source span instead of the whole declaring type:

```powershell
$q = "<skillRoot>\scripts\query.ps1"
& $q find-type   CharacterDomain -Assembly GameData -Limit 20
& $q find-member GetTaiwuCharId  -Assembly GameData -Limit 20
& $q member      GameData.Domains.Taiwu.TaiwuDomain GetTaiwuCharId -Assembly GameData
& $q member      -Token 0x0600589E -Assembly GameData
```

Do not load full decompiled types into context. Inspect only the needed type/member and the nearby call context.

## Decompiler Worker

On Windows, the worker is `tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe`. It reads the installed DLLs directly; there is no index or cache to build first:

```powershell
$worker = "tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe"
$frontend = "<gameRoot>\The Scroll of Taiwu_Data\Managed"
$backend = "<gameRoot>\Backend"

& $worker --command scan --managed-dir $frontend
& $worker --command scan --managed-dir $backend
& $worker --command export-core --managed-dir $backend --out-dir <repoRoot>\artifacts\decompiled\backend
& $worker --command decompile-type --managed-dir $backend --assembly GameData --type GameData.Domains.Character.CharacterDomain
& $worker --command decompile-member --managed-dir $backend --assembly GameData --token 0x06000000
```

For reference Mod DLLs, point `--managed-dir` at their plugin folder and keep the export outside the Mod root:

```powershell
& $worker --command export-dir --managed-dir <referenceMod>\Plugins --out-dir <repoRoot>\artifacts\reference-decompiled\<modId>
```

Treat decompiled output as evidence, not as source to copy wholesale. Exports are the only cache in this toolchain; refresh them after a game update by comparing the fingerprints in `summary.json`.

## Build References

The compiler resolves references from the `--game-libs` directory, and picks the side's required set by probing for `Assembly-CSharp.dll`:

- Frontend directory: `Assembly-CSharp.dll`, `GameData.Shared.dll`, `TaiwuModdingLib.dll`, `0Harmony.dll`, `UnityEngine.CoreModule.dll`.
- Backend directory: `GameData.dll`, `GameData.Shared.dll`, `TaiwuModdingLib.dll`, `0Harmony.dll`.
- Every other managed DLL in the directory is added automatically; non-.NET files are skipped.

Consequences:

- Pass the frontend directory for a frontend build and `<gameRoot>\Backend` for a backend build. A mixed directory compiles and then fails at runtime.
- `GameData.Shared.dll` exists on both sides with different content; always take the copy from the side you are building.
- Do not copy game DLLs into the Mod and do not add a `.csproj`; the worker is the build system.

## Harmony Target Evidence

Before emitting a patch, capture:

- Side: frontend or backend.
- Assembly name.
- Full type name.
- Method name or constructor kind.
- Parameter type list.
- Method token when available.
- Why Prefix/Postfix/Transpiler/Finalizer is appropriate.
