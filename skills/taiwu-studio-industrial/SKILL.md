---
name: taiwu-studio-industrial
description: Industrial refactoring and feature-development guidance for the Rust + egui Taiwu Studio project, a Mod IDE for The Scroll of Taiwu. Use when Codex works on Taiwu Studio architecture, UI workbench design, Harmony mod workflow, C# worker integration, project scanning, SQLite/database indexing, build/deploy pipelines, roadmap/plan updates, verification screenshots, or commit-cache discipline.
---

# Taiwu Studio Industrial

Use this skill when working in `C:\Users\Administrator\Documents\GitHub\taiwu_studio`.

## Ground Rules

- Treat Taiwu Studio as a professional Mod IDE, not a demo panel collection.
- Keep Harmony as a first-class workflow: patch templates, target lookup, conflict analysis, and `0Harmony.dll` validation.
- Use a Codex-like light gray theme unless the user explicitly requests another theme.
- Keep game path, workspace, theme, fonts, and build toolchain in Settings, not Inspector.
- Keep the right side as contextual Inspector only.
- Update `docs/PLAN.md` when execution policy changes.
- Append `docs/COMMIT_CACHE.md` after each coherent step. Do not create git commits.
- Run `cargo fmt`, `cargo check`, and for UI work `cargo build` plus HWND screenshot verification.

## Verification

- For UI verification on Windows, prefer stable HWND capture:
  - enumerate visible windows for `taiwu_studio`;
  - choose title `Taiwu Studio` with a sane size;
  - use DWM extended frame bounds for bitmap size;
  - call `PrintWindow(hwnd, hdc, 2)`;
  - do not use plain screen rectangle capture for final verification.
- For custom title bars, verify dragging with a real pointer or Win32 mouse event and compare window rect before/after.
- Keep screenshots in `target/verification/`.

## Architecture Direction

- Rust/egui owns workbench UI, project model, settings, and task orchestration.
- SQLite index owns searchable local metadata:
  - mod manifests;
  - files;
  - diagnostics;
  - Harmony patches;
  - future game API/member indexes.
- Heavy C# build, assembly indexing, and decompilation work should go through worker processes instead of blocking egui.

## References

Read `references/index-schema.md` when implementing or modifying the SQLite index.
