---
name: egui-034-frontend
description: Build, refactor, debug, migrate, and verify Rust desktop frontends using egui/eframe 0.34.x. Use when Codex upgrades egui/eframe to 0.34, consults the local egui clone, fixes custom window chrome, themes egui widgets, handles ViewportCommand behavior, resolves 0.34 API changes such as CornerRadius or StrokeKind, registers fonts for Chinese UI, or verifies egui windows with HWND screenshots.
---

# egui 0.34 Frontend

Use this skill for Rust desktop UI work based on `egui = "0.34.3"` and `eframe = "0.34.3"`.

## Required Context

- Local egui clone: `C:\Users\Administrator\Documents\GitHub\deltawater\egui`
- Prefer the local clone over memory or web snippets when API details matter.
- Check the app's existing widget helpers, theme setup, and viewport configuration before changing UI.
- For Taiwu Studio, pair this skill with `taiwu-studio-industrial` when changing the IDE shell, theme, settings, run/build panels, Harmony workbench, or verification workflow.

## Core Workflow

1. Identify the project's egui/eframe versions in `Cargo.toml` and `Cargo.lock`.
2. If API behavior is uncertain, search the local clone before editing.
3. Preserve the app's local UI helpers and theme abstractions instead of spreading one-off styling.
4. After changes, run the project's normal Rust checks.
5. For visual or window-chrome work, launch the executable and capture by HWND.

## Source Lookup

Use `rg` in the local egui clone before fixing compiler errors, migration breaks, or viewport behavior.

- Viewport and window commands: `crates/egui/src/viewport.rs`
- Core style and visuals: `crates/egui/src/style.rs`
- Layout, allocation, interaction: `crates/egui/src/ui.rs`
- Widgets and response behavior: `crates/egui/src/widgets/`
- eframe integration and native runner: `crates/eframe/src/`
- Version features and examples: workspace `Cargo.toml` and `examples/`

## Implementation Rules

- Keep the project's dependency pair aligned: `egui = "0.34.3"` and `eframe = "0.34.3"`.
- Use `cargo update -p egui -p eframe` when the lockfile keeps older versions.
- Prefer theme-level `Visuals`, `Style`, spacing, and reusable helper widgets over scattered color overrides.
- Give fixed or computed dimensions to title bars, activity bars, side bars, tabs, status bars, icon buttons, and dense tool rows.
- Use icon-only controls where the action is conventional, with hover text for non-obvious actions.
- Avoid pure white panels for this project; use Codex-like light gray surfaces unless the user asks for another theme.
- Keep text readable in Chinese by preserving UTF-8 files and verifying rendered UI after font or theme changes.
- Prefer `CornerRadius` APIs over deprecated `Rounding`; use `StrokeKind` with painter stroke calls that require it.
- Keep custom widgets accessible through stable hover, active, focused, disabled, and selected states.

## Custom Window Chrome

- Disable native decorations through `ViewportBuilder` when custom chrome is required.
- Separate drag regions from button and menu regions.
- Send `egui::ViewportCommand::StartDrag` immediately after primary press inside the drag region.
- Keep minimize, maximize/restore, close, menus, tabs, and splitters outside the drag region.
- For rounded corners on Windows, verify both DWM attributes and the fallback window region when present.

## Verification

- Run `cargo fmt`, `cargo check`, and `cargo build` after code changes.
- For visual work, launch the built executable and verify with a real HWND capture.
- Use DWM extended frame bounds plus `PrintWindow`; do not accept guessed desktop rectangle screenshots.
- Re-check drag behavior after any title-bar, menu-bar, or viewport change.

## References

Read `references/source-map.md` when an egui/eframe 0.34 API detail, source path, migration, widget behavior, or dependency mismatch matters.
Read `references/migration-034.md` when compiler errors or stale snippets look like an egui 0.2x/0.3x to 0.34 migration issue.
Read `references/taiwu-ui-patterns.md` when working on Taiwu Studio's workbench composition, Codex-like light theme, buttons, panels, or Chinese text rendering.
Read `references/window-verification.md` when implementing custom window chrome, rounded corners, drag behavior, or screenshot QA.

## Scripts

Use `scripts/capture-egui-window.ps1` for repeatable Windows HWND screenshots and optional drag verification when a project has no better local capture script.
