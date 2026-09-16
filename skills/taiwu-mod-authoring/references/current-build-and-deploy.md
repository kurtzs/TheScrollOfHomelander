# Current Build And Deploy

Use this reference for the installed Taiwu Studio toolchain. Resolve paths first; do not assume the example user profile or Steam library is valid on another machine.

## Resolve Inputs

```powershell
$gameRoot = "C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu"
$frontendLibs = Join-Path $gameRoot "The Scroll of Taiwu_Data\Managed"
$backendLibs = Join-Path $gameRoot "Backend"
$worker = "tools\TaiwuStudio.RoslynWorker\bin\Release\net8.0\TaiwuStudio.RoslynWorker.exe"
$modRoot = "<path to the Mod project>"
$steamMod = Join-Path $gameRoot "Mod\TheScrollOfHomelander"
$appDataMod = "<optional AppData development Mod copy>"
```

Before building, confirm `Test-Path` for the selected library root and worker, inspect `Config.lua`, and make sure the target game is not being used as the source of an unrelated Mod copy.

## Roslyn Worker

The worker collects C# under `Scripts/` and references the game libraries. It writes the requested path under the project's `Plugins` directory, so pass `Front\Name.dll`, not `Plugins\Front\Name.dll`.

```powershell
& $worker build `
  --project-root $modRoot `
  --game-libs $frontendLibs `
  --assembly-name BetterTaiwuScrollFrontend `
  --output-plugin-path "Front\BetterTaiwuScrollFrontend.dll" `
  --source-dir "Scripts\Frontend" `
  --configuration Release
```

Build backend separately with `$backendLibs`, `BetterTaiwuScrollBackend`, `Back\BetterTaiwuScrollBackend.dll`, and `Scripts\Backend`. Treat `success=false` or any error diagnostic as a failed build. Warnings about duplicate `using` directives or obsolete game APIs still deserve review but do not replace error checking.

Do not deploy the accidental `Plugins\Plugins\...` output produced by an incorrect output path. Do not copy game DLLs, `bin`, `obj`, `.taiwu-studio`, source archives, or decompiler caches.

## Deployment

Identify the actual `Mod\TheScrollOfHomelander` directory from the running installation. Keep a separate AppData development copy only when the game loader is known to use it. Copy the built DLL and PDB to the declared `Plugins\Front` or `Plugins\Back` path. Copy Config/assets only when they are intentionally changed.

Preserve user configuration: compare the destination `Config.lua` and make a targeted version/path edit when needed instead of blindly replacing a customized file. Do not delete old files unless the user explicitly requested cleanup. If the game is running, a successful file copy does not update the already loaded assembly; require a full restart.

## Hash And Load Check

```powershell
Get-FileHash $builtDll -Algorithm SHA256
Get-FileHash (Join-Path $steamMod "Plugins\Front\BetterTaiwuScrollFrontend.dll") -Algorithm SHA256
Get-FileHash (Join-Path $appDataMod "Plugins\Front\BetterTaiwuScrollFrontend.dll") -Algorithm SHA256
```

All intended copies should match the build output. After a complete restart, inspect `Player.log` for `Loading plugin from ...`, the Mod ID, Harmony target failures, assembly load errors, and feature-specific diagnostics. A hash match without a fresh load is not runtime verification.
