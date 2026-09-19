# Build And Deploy

The only sanctioned build entry point is `mods\TheScrollOfHomelander\Build-Deploy.ps1`. It wraps `tools\TaiwuStudio.RoslynWorker` (the file name contains "TaiwuStudio"; the tool itself is a standalone .NET 8 console app).

## What The Script Does

1. Defines the two sides: `Frontend` → `The Scroll of Taiwu_Data\Managed`, output `Plugins\Front\BetterTaiwuScrollFrontend.dll`; `Backend` → `Backend`, output `Plugins\Back\BetterTaiwuScrollBackend.dll`.
2. For each side, runs the Roslyn worker with `--source-dir Scripts/<Side> --source-dir Scripts/Shared --configuration Release`, saves the raw JSON to `build-records\<Side>.json`, and aborts the whole run (`throw`) if the exit code is non-zero, `success` is false, or any diagnostic has severity `error`. Warnings are counted and printed but do not fail the build.
3. Hashes every file under `Scripts\`, plus `Build-Deploy.ps1`, `Config.lua`, `Settings.Lua`, then hashes the four game assemblies (`Assembly-CSharp.dll`, `GameData.Shared.dll` from `Managed`; `GameData.dll`, `GameData.Shared.dll` from `Backend`), then the built `Plugins\**\*.dll|*.pdb`.
4. Writes `release-manifest.json` with `schemaVersion`, `createdUtc`, `sourceRevision` (git HEAD of the repo root), `sourceDigest`, `sourceFiles[]`, `gameAssemblies[]`, `plugins[]`, and a `validation` note stating that only independent compilation and static API inspection were performed.
5. Prints `Source digest: <sha256>`.

## Deploy Behaviour (`-Deploy`)

- Aborts if `<gameRoot>\Mod\TheScrollOfHomelander\Config.lua` is missing.
- Backs up the whole Steam Mod directory to `Documents\TaiwuModBackups\<yyyyMMdd-HHmmss>-quality-<8 hex>` first.
- Publishes to **two** destinations: the Steam Mod root and `%LOCALAPPDATA%\TaiwuStudio\ModDevelopment\TheScrollOfHomelander-quality`. A destination that does not exist yet additionally receives `Config.lua`, `Settings.Lua`, `Assets`, `GradeBackgrounds`, `icon.png`; an existing one is not overwritten with those assets, which protects local configuration.
- Copies all `Scripts\**\*.cs`, then the plugin DLL/PDB through a temp file + `File.Replace`, then the manifest.
- Re-verifies SHA256 of every deployed plugin and `Scripts\*` file against the manifest and throws `Deployment hash mismatch: <path>` on any difference.
- Prints `Deployed and hash-matched: <destination>` and `Backup: <path>`.

## Parameters

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1 `
  -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu' `
  -Deploy `
  -DevelopmentRoot (Join-Path $env:LOCALAPPDATA 'TaiwuStudio\ModDevelopment\TheScrollOfHomelander-quality') `
  -BackupRoot (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'TaiwuModBackups')
```

Run it from the repository with PowerShell 7. Requires the Release Roslyn worker (`tools\TaiwuStudio.RoslynWorker\bin\Release\net8.0\TaiwuStudio.RoslynWorker.exe`) and the installed game libraries.

## Worker Flags, For Reference Or Manual Builds

```powershell
$worker = "tools\TaiwuStudio.RoslynWorker\bin\Release\net8.0\TaiwuStudio.RoslynWorker.exe"
& $worker build --project-root <modRoot> --game-libs <libsDir> --assembly-name <name> `
  --output-plugin-path "Front\BetterTaiwuScrollFrontend.dll" `
  --source-dir "Scripts\Frontend" --source-dir "Scripts\Shared" --configuration Release
```

- The side is inferred from the library directory: if `Assembly-CSharp.dll` is present the worker uses the frontend reference set, otherwise the backend set.
- `--source-dir` is repeatable; with none given it compiles everything under `Scripts\`. Passing `Scripts\Shared` explicitly is required for shared types to land in both assemblies.
- `--output-plugin-path` is relative to `<project-root>\Plugins`; passing `Plugins\Front\X.dll` produces the wrong `Plugins\Plugins\Front\X.dll`.
- The worker prepends `using` directives for every namespace found in the referenced `Assembly-CSharp*` / `GameData.*` assemblies, so new source files do not need using statements for game namespaces. Keep explicit usings only for `System.*`, Harmony, Unity, and aliases.
- Preprocessor symbols are `TAIWU_MOD`, `UNITY_STANDALONE_WIN`, `UNITY_64`, plus `DEBUG`/`RELEASE`.
- References come from the game library directory; only a `.csproj` in the project root would add `HintPath` references, and this Mod deliberately has none.

## Failure Modes

| Symptom | Meaning |
| --- | --- |
| `Required reference assembly not found: <name>` (TSW002) | Wrong `--game-libs` directory for that side, or a missing game DLL |
| `No C# source files found under Scripts/.` (TSW001) | Wrong `--source-dir` spelling |
| `success=false` with C# errors | Real compile errors; the script aborts before deploying anything |
| `Deployment hash mismatch` | A file changed between manifest creation and copy, or a copy was blocked (game running, antivirus) |
| Build succeeds but the game behaves the same | The game process still holds the old assembly — a full restart is required |

## After Deploying

Tell the user to close the game completely and restart it, then check `Player.log` for `[BetterTaiwuScroll]` lines, `Installed patch group: <group>`, and the absence of `Disabled patch group`. A successful copy or hash match is not runtime verification; this workspace never launches the game.
