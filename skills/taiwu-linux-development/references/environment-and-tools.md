# Environment And Tools

## Locations

| Purpose | Linux path |
| --- | --- |
| Workspace | `/run/media/shishanyue/Win11Pro X64/Users/Administrator/Documents/TheScrollOfHomelander-Workspace` |
| Mod source | `<workspace>/mods/TheScrollOfHomelander` |
| Game | `/home/shishanyue/.local/share/Steam/steamapps/common/The Scroll Of Taiwu` |
| Frontend references | `<game>/The Scroll of Taiwu_Data/Managed` |
| Backend references | `<game>/Backend` |
| Steam manifest | `/home/shishanyue/.local/share/Steam/steamapps/appmanifest_838350.acf` |
| Worker source | `<workspace>/tools/TaiwuStudio.{RoslynWorker,DecompilerWorker}` |
| Linux build outputs | `<workspace>/tools/.linux/bin/TaiwuStudio.<Name>Worker/release` |
| Historical source, read-only | `/run/media/shishanyue/Win11Pro X64/Users/Administrator/Documents/GitHub/taiwu_studio/tools` |

Case matters: the installation directory contains `Of`, the frontend data directory contains `of`. Preserve the Mod file name `Settings.Lua`. Do not convert Linux paths to `C:\...`, use `%LOCALAPPDATA%`, or execute the shipped Windows `.exe` apphosts.

The archived tool sources matched the workspace byte-for-byte at setup, so no import was needed. If recovery is later necessary, compare specific source files first and bring only required changes into this workspace; never build in or modify the archive.

## Build The Workers

Prerequisites: Bash, a .NET SDK 8 or newer, and NuGet access (or cached packages and .NET 8 targeting packs). This host has SDK 10; `dotnet build` alone would otherwise reuse Windows `obj` state and replace tracked Release binaries.

From the workspace root:

```bash
dotnet --info
bash tools/build-linux.sh
bash tools/run-worker.sh decompiler --help
```

The build script retains `net8.0` and package versions, uses `--artifacts-path tools/.linux` and `-p:UseAppHost=false`, and stops on any failed project. It neither needs game files nor installs system packages. No `chmod` or NTFS executable permissions are needed when invoking these scripts with Bash.

The launcher uses `dotnet exec --roll-forward Major` by default. This prefers .NET 8 when present but permits a newer major when it is absent; the setting is local to the tool process, not exported to Steam or the game. An existing `DOTNET_ROLL_FORWARD` explicitly overrides it. Install the .NET 8 runtime if strict runtime matching is needed. Do not retarget the Mod to the host SDK.

Both workers accept CLI arguments, not JSON requests. Roslyn emits pretty-printed JSON with `success` and `diagnostics`, and exits 0/1/2 for success/build failure/invalid invocation. Decompiler work commands emit JSON with `ok`, `payload`, `diagnostics`; its help is plain text and exits 0. Account for a possible UTF-8 BOM when parsing captured output.

## Smoke Checks Without The Game

```bash
bash tools/run-worker.sh decompiler --command find-type \
  --managed-dir "$PWD/tools/.linux/bin/TaiwuStudio.RoslynWorker/release" \
  --assembly TaiwuStudio.RoslynWorker.dll --query CompileDiagnostic --limit 1

bash tools/run-worker.sh decompiler --command decompile-type \
  --managed-dir "$PWD/tools/.linux/bin/TaiwuStudio.RoslynWorker/release" \
  --assembly TaiwuStudio.RoslynWorker.dll --type CompileDiagnostic

# Expected exit 2 and JSON diagnostic TSW000, not a failed installation.
bash tools/run-worker.sh roslyn
```

For a missing-reference negative check, use a deliberately nonexistent path under the tool artifacts, never a game directory that Steam may populate concurrently. These checks do not execute game code or prove a Mod compiles.

## Skill Discovery

Project `opencode.json` registers `skills.paths = ["./skills"]`, exposing this skill and the three companions without copies or symlinks. Inspect with `opencode debug skill`. Restart OpenCode after changing skills/config; the running session retains its original discovery list. No global OpenCode configuration is changed.
