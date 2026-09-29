# Current Build And Deploy

Use this reference for the installed worker toolchain. Resolve paths first; do not assume the example Steam library or user profile is valid on another machine.

The PowerShell commands and profile paths below are Windows-specific. On Linux, follow [taiwu-linux-development](../../taiwu-linux-development/SKILL.md): build the workers with `bash tools/build-linux.sh`, then use `bash tools/run-worker.sh roslyn build ...` against validated game libraries. Worker output is `tools/.linux/bin/TaiwuStudio.<Name>Worker/release/`; the workers retain `net8.0`.

`Build-Deploy.ps1` is Windows-only as currently implemented; changing `-GameRoot` does not fix its worker path or profile assumptions. Direct Linux worker builds do not provide the full release-manifest, backup, deployment, and hash-verification pipeline.

## Resolve Inputs

```powershell
$gameRoot = "C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu"
$frontendLibs = Join-Path $gameRoot "The Scroll of Taiwu_Data\Managed"
$backendLibs = Join-Path $gameRoot "Backend"
$worker = "<repoRoot>\tools\TaiwuStudio.RoslynWorker\bin\Release\net8.0\TaiwuStudio.RoslynWorker.exe"
$modRoot = "<path to the Mod project>"
$steamMod = Join-Path $gameRoot "Mod\TheScrollOfHomelander"
$devCopy = Join-Path $env:LOCALAPPDATA "TaiwuStudio\ModDevelopment\TheScrollOfHomelander-quality"
```

Before building, confirm `Test-Path` for the selected library root, required side-specific DLLs, and the worker, inspect `Config.lua`, and make sure the game install is not being used as the source of an unrelated Mod copy. An existing but empty directory is not a validated installation. Only the Steam Mod root is loaded by the game; the `%LOCALAPPDATA%` development copy is a working mirror that a deploy script may maintain.

## Roslyn Worker

The worker collects C# under `Scripts/` and references the game libraries. It writes the requested path under the project's `Plugins` directory, so pass `Front\Name.dll`, not `Plugins\Front\Name.dll`.

```powershell
& $worker build `
  --project-root $modRoot `
  --game-libs $frontendLibs `
  --assembly-name BetterTaiwuScrollFrontend `
  --output-plugin-path "Front\BetterTaiwuScrollFrontend.dll" `
  --source-dir "Scripts\Frontend" `
  --source-dir "Scripts\Shared" `
  --configuration Release
```

Build backend separately with `$backendLibs`, `BetterTaiwuScrollBackend`, `Back\BetterTaiwuScrollBackend.dll`, and `--source-dir Scripts\Backend` plus the same `--source-dir Scripts\Shared`. Passing the shared directory for **both** sides is required: shared types must exist in both assemblies, and omitting it fails the build with missing-type errors. Treat `success=false` or any error diagnostic as a failed build. Warnings about duplicate `using` directives or obsolete game APIs still deserve review but do not replace error checking.

Do not deploy the accidental `Plugins\Plugins\...` output produced by an incorrect output path. Do not copy game DLLs, `bin`, `obj`, source archives, or decompiled exports.

The compiler currently fixes `UNITY_STANDALONE_WIN` regardless of host OS. A Linux host build does not establish native Linux game support; confirm whether the target is Windows/Proton or native Linux.

## Deployment

Identify the actual `Mod\TheScrollOfHomelander` directory from the running installation. The Steam Mod root is the only destination the game loads; an AppData development copy is a mirror that must be updated explicitly. Copy the built DLL and PDB to the declared `Plugins\Front` or `Plugins\Back` path. Copy Config/assets only when they are intentionally changed.

Preserve user configuration: compare the destination `Config.lua` and make a targeted version/path edit when needed instead of blindly replacing a customized file. Do not delete old files unless the user explicitly requested cleanup. If the game is running, a successful file copy does not update the already loaded assembly; require a full restart.

## Hash And Load Check

```powershell
Get-FileHash $builtDll -Algorithm SHA256
Get-FileHash (Join-Path $steamMod "Plugins\Front\BetterTaiwuScrollFrontend.dll") -Algorithm SHA256
Get-FileHash (Join-Path $devCopy "Plugins\Front\BetterTaiwuScrollFrontend.dll") -Algorithm SHA256
```

All intended copies should match the build output. After a complete restart, inspect `Player.log` for `Loading plugin from ...`, the Mod ID, Harmony target failures, assembly load errors, and feature-specific diagnostics. A hash match without a fresh load is not runtime verification.
