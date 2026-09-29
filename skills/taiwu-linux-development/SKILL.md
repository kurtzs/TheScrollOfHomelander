---
name: taiwu-linux-development
description: The Scroll of Taiwu and TheScrollOfHomelander Mod development on Linux, including Steam/Proton paths, rebuilding RoslynWorker and DecompilerWorker, current-assembly API queries, separate frontend/backend compilation, and safe release gates. Use when developing, reviewing, debugging, building, or preparing deployment in this workspace from Linux; combine with the existing Taiwu authoring, API, and project skills.
---

# Taiwu Linux Development

Use this as the Linux environment and command entrypoint, not as a replacement for the existing Mod engineering rules. Read the workspace `AGENTS.md` first. All relative commands below assume the workspace root as working directory; quote paths because both the workspace and game names contain spaces.

## Load The Relevant Detail

| Task | Companion skill / reference |
| --- | --- |
| Feature, settings, Harmony, side ownership | [taiwu-mod-authoring](../taiwu-mod-authoring/SKILL.md) |
| Installed API, overload, token, lifecycle | [taiwu-decompiled-api](../taiwu-decompiled-api/SKILL.md) |
| This Mod's layout, patch groups, JSON stores | [the-scroll-of-homelander-mod](../the-scroll-of-homelander-mod/SKILL.md) |
| Linux paths, .NET, tool rebuild and smoke checks | [environment-and-tools.md](references/environment-and-tools.md) |
| Current DLL queries and isolated Mod compilation | [api-and-build.md](references/api-and-build.md) |
| Proton data, backups, release/deployment restrictions | [runtime-and-deploy.md](references/runtime-and-deploy.md) |

The companion skills retain Windows examples. On Linux, use this skill's commands instead of `.exe`, `query.ps1`, or `Build-Deploy.ps1`. Changing only `-GameRoot` does not make those PowerShell scripts portable.

## Development Loop

1. Inspect the worktree and preserve unrelated edits. Work only in this workspace, never the archived `Documents/GitHub/taiwu_studio` checkout. Existing `.dsh/skills` Windows links may be unreadable on Linux; use canonical `skills/` files, not those links.
2. Recheck installation readiness. Default game root is `/home/shishanyue/.local/share/Steam/steamapps/common/The Scroll Of Taiwu`. Require the correct side's actual DLLs; an existing or partially downloaded directory is not an installed API. Stop game-dependent work if references are missing, but tool builds and self-tests can proceed.
3. Build or refresh workers with `bash tools/build-linux.sh`. Run them with `bash tools/run-worker.sh decompiler ...` or `bash tools/run-worker.sh roslyn build ...`. Outputs and restore state live under ignored `tools/.linux/`; shipped Windows binaries remain untouched.
4. Resolve exact types/members from the installed assemblies before editing code. Record side, DLL hash, full type, overload, token, and the relevant source span. A zero-result query may be an API change, not a broken tool. Never invent replacement type names from old source.
5. Implement the smallest change using the companion skills' architecture and safety rules. Keep frontend UI separate from authoritative backend state, preserve disabled-feature behavior, and retain persistent JSON compatibility. Never automatically retry a submitted gameplay mutation after timeout.
6. Compile frontend and backend separately through Roslyn, including `Scripts/Shared` in both. Use isolated output and retain each JSON diagnostic result. A successful backend build does not excuse a failing frontend or establish game compatibility.
7. Treat a development compile as a check, not a release. Do not publish partial, stale, or unmanifested DLLs. Linux release/deployment automation is not yet ported; follow the explicit gates in [runtime-and-deploy.md](references/runtime-and-deploy.md).
8. Report exactly what was checked and what remains blocked. Never launch the game, Steam, Proton, or in-game automation for acceptance. After an authorized deployment, ask the user to fully exit and restart the game, then supply the relevant logs and observations.

## Host Is Not Target

This is Linux-hosted development against the Windows game installed by Steam. The current compiler defines `UNITY_STANDALONE_WIN` and produces AnyCPU DLLs using game references; leave the symbol unchanged merely because the host is Linux. The workers' `net8.0` target is not the Mod's runtime target. Do not add .NET 10 runtime references to a Mod or copy game assemblies into its package.

## Verified Baseline

Snapshot from 2026-09-29, not a permanent assumption:

- Steam app `838350`, build `25596993`, manifest `StateFlags=4`; the initially empty installation completed during setup. The game is a Windows x86-64 build and a Proton prefix exists, but game startup was not tested.
- Both workers rebuilt with SDK `10.0.111`, zero warnings/errors. Their unchanged `net8.0` binaries ran on runtime `10.0.11` with process-local `Major` roll-forward. Six worker smoke checks and real frontend/backend type searches passed.
- The current Mod backend compiled with zero errors and 140 warnings. Frontend compilation failed with 85 errors and 217 warnings, including missing `UI_Make` and `UI_BuildingManage`. This is a compatibility follow-up, not permission to change unrelated features during skill maintenance.
- No release or deployment was performed. The Steam `Mod` directory was empty; `Mod/TheScrollOfHomelander` did not exist. No runtime log or Mod JSON directory had been created in the prefix.

Recheck this baseline after Steam updates or source changes. Never claim the Mod is ready to deploy from tool-build success alone.
