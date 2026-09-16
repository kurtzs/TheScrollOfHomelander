# API And Tooling

Use this reference before searching APIs, selecting patch targets, or decompiling game code.

## Taiwu Studio API Browser

After setting the game root, rebuild the API index. Use filters early:

- Side: `frontend` for Unity/UI/Managed APIs; `backend` for `GameData` and domain APIs.
- Kind: `type`, `method`, `field`, `property`.
- Assembly: narrow to `Assembly-CSharp`, `TaiwuModdingLib`, `0Harmony`, `GameData`, or a specific `GameData.*`.
- Namespace: narrow to `GameData.Domains`, `GameData.Domains.Character`, frontend UI namespaces, etc.

Recommended flow:

1. Search a likely type name.
2. Filter to the intended side.
3. Filter to a symbol kind.
4. Inspect the selected type/member.
5. Decompile a narrow span.
6. Check overload parameters and method token before generating a Harmony target.

## taiwu-decompiled-api Skill

Use `taiwu-decompiled-api` for real type/member/source details:

```powershell
py -3 C:\Users\Administrator\.codex\skills\taiwu-decompiled-api\scripts\find_type.py CharacterDomain
py -3 C:\Users\Administrator\.codex\skills\taiwu-decompiled-api\scripts\find_member.py GetCharacter
py -3 C:\Users\Administrator\.codex\skills\taiwu-decompiled-api\scripts\show_source.py GameData.Domains.Character.CharacterDomainHelper --context 80
py -3 C:\Users\Administrator\.codex\skills\taiwu-decompiled-api\scripts\find_patch_targets.py SetHealth
```

Useful overrides:

```powershell
$env:TAIWU_STUDIO_INDEX = "C:\Users\Administrator\AppData\Roaming\TaiwuStudio\Taiwu Studio\data\index.sqlite3"
$env:TAIWU_DECOMPILED_CACHE = "C:\Users\Administrator\AppData\Roaming\TaiwuStudio\Taiwu Studio\data\.taiwu-studio\decompiled"
```

Do not load full decompiled trees into context. Inspect only the needed type/member and nearby call context.

## Decompiler Worker

Taiwu Studio uses `tools\TaiwuStudio.DecompilerWorker`:

```powershell
$worker = "tools\TaiwuStudio.DecompilerWorker\bin\Debug\net8.0\TaiwuStudio.DecompilerWorker.exe"
$frontend = "C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\The Scroll of Taiwu_Data\Managed"
$backend = "C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\Backend"

& $worker --command scan --managed-dir $frontend
& $worker --command scan --managed-dir $backend
& $worker --command export-core --managed-dir $backend --out-dir target\backend-api
& $worker --command decompile-type --managed-dir $backend --assembly GameData --type GameData.Domains.Character.CharacterDomain
& $worker --command decompile-member --managed-dir $backend --assembly GameData --token 0x06000000
```

For reference Mod DLLs:

```powershell
& $worker --command export-dir --managed-dir docs\reference\3746087946\Plugins --out-dir target\reference-decompiled\3746087946
```

Treat decompiled output as evidence, not as source to copy wholesale.

## Build References

Frontend projects should reference frontend Managed assemblies. Backend projects should reference backend assemblies. Mixed references often compile and then fail at runtime.

Check:

- `TaiwuModdingLib.dll` is referenced where plugin entrypoints are used.
- `0Harmony.dll` is referenced when using Harmony.
- Unity/UI assemblies appear only in frontend projects.
- `GameData.*` domain assemblies appear only in backend projects unless the specific assembly is known to be side-safe.
- Third-party or framework Mod SDK DLLs are copied only when the game does not provide them.

## Harmony Target Evidence

Before emitting a patch, capture:

- Side: frontend or backend.
- Assembly name.
- Full type name.
- Method name or constructor kind.
- Parameter type list.
- Method token when available.
- Why Prefix/Postfix/Transpiler/Finalizer is appropriate.
