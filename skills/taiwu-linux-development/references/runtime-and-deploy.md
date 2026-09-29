# Runtime Data And Deployment Gates

## Proton Paths

The installed Steam manifest identifies app ID `838350`. At setup an actual prefix existed at:

```text
/home/shishanyue/.local/share/Steam/steamapps/compatdata/838350/pfx
```

Its recorded configuration referenced Proton Experimental. This does not prove that prerequisites, rendering, the game, or the Mod work. Do not launch Steam, Proton, the game executable, or game automation to find out; runtime acceptance belongs to the user.

Candidate runtime paths inside that prefix:

```text
drive_c/users/steamuser/Documents/TheScrollOfHomelander/*.json
drive_c/users/steamuser/AppData/LocalLow/Conchship/The Scroll of Taiwu/Player.log
```

`Conchship` and `The Scroll of Taiwu` come from the installed `The Scroll of Taiwu_Data/app.info`. Prefix Documents existed as a real directory at setup, but neither the Mod JSON directory nor Player.log existed. After the user runs the game, verify the actual files, timestamps and any symlink/redirection; do not treat these candidates as confirmed active paths or replace them with host `$HOME/Documents` blindly.

Read `Scripts/Frontend/Shared/ModUserDataPaths.cs` for Documents/Personal/fallback resolution. The old mounted Windows directory `C:\Users\Administrator\Documents\TheScrollOfHomelander` also remains protected runtime data, not a source tree. Never migrate, overwrite, or seed runtime JSON, saves, Steam Cloud data, or prefix registry files as part of building tools.

## No Implicit Linux Deployment

The desired game-side Mod root is `<game>/Mod/TheScrollOfHomelander`, not the workspace, Steam Workshop cache, Proton Documents, or a guessed AppData development mirror. At setup the game's `Mod` directory was empty. This task creates development skills and tools only; it does not install the Mod.

The existing `mods/TheScrollOfHomelander/Build-Deploy.ps1` is still a **Windows** release pipeline. It depends on a Windows apphost and environment paths; installing `pwsh` or supplying Linux `-GameRoot` alone is insufficient. Its `-Deploy` also requires an existing Steam Mod `Config.lua`, so it cannot bootstrap this fresh installation.

Before a future Linux release/deploy implementation or manual release, require all of the following:

- Explicit deployment scope and confirmed target. Both frontend and backend must compile against the same stable, currently installed game build with zero errors. Do not deploy the backend-only check artifact.
- A release manifest covering source revision plus dirty source hashes, build configuration, game assembly SHA256, and the exact DLL/PDB hashes. Direct development compilation does not create this evidence; never reuse the old manifest with new binaries.
- Full backup of any existing target before modification, to a new timestamped location outside the target. Prefer host `$HOME/.local/state/TaiwuModBackups` (or an explicitly chosen backup root), not the mounted Windows userdata directory. Verify backup completeness before publishing.
- An explicit plugin allowlist matching `Config.lua`: `Plugins/Front/BetterTaiwuScrollFrontend.dll` and `.pdb`, `Plugins/Back/BetterTaiwuScrollBackend.dll` and `.pdb`. Do not copy every historical file under `Plugins`, worker DLLs, game references, `bin`, or `obj`.
- Intentional handling of a fresh target versus an existing target. A fresh installation needs reviewed `Config.lua`, `Settings.Lua`, and required assets. Existing configuration/assets require preservation or a reviewed merge; never overwrite runtime JSON or unrelated files.
- Per-file temporary writes/replacement, SHA256 comparison for every published file, and a clear rollback path. Per-file atomic replacement is not an atomic multi-file release. Do not delete extra target files with `rsync --delete` or similar broad sync operations.
- No default development mirror on Linux. Add one only when requested, with its path and preservation policy explicit.

After authorized deployment, tell the user to fully exit and restart the game so Unity reloads the DLLs. Ask for plugin-load and Harmony diagnostics in Player.log, and manual normal/disabled/repeated-refresh/close/reload checks. Runtime success must not be inferred from hashes or successful compilation.
