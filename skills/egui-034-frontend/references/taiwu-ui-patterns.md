# Taiwu Studio egui UI Patterns

Use this reference for Taiwu Studio and similar industrial desktop IDEs.

## Workbench Shape

- Use an IDE shell: activity bar, primary side bar, central editor/workbench, contextual inspector, bottom panel, and status bar.
- Keep the first screen as a workbench/welcome hub, not a single project form.
- Keep game path, workspace, theme, fonts, and toolchain settings in Settings.
- Keep the right side contextual: selected file, symbol, patch, task, or diagnostics only.
- Treat Harmony as a first-class workflow in navigation, templates, conflict inspection, and target lookup.

## Theme

- Use a Codex-like light gray theme by default.
- Avoid pure white panels and avoid a dark-only palette unless explicitly requested.
- Use subtle borders and layered grays for hierarchy:
  - app background;
  - side/activity surfaces;
  - editor/workbench surface;
  - hovered and active controls;
  - disabled/secondary text.
- Buttons need clear hover, active, focused, disabled, and selected states.
- Keep rounded corners modest and consistent; cards are for repeated items, dialogs, and framed tools, not nested page sections.

## Layout Rules

- Set stable widths/heights for title bars, side bars, activity icons, tab rows, status bars, and tool buttons.
- Avoid text overlap by constraining labels, using ellipsis where appropriate, and reserving enough height for Chinese glyphs.
- Do not put global settings in the Inspector to fill space.
- Keep dense IDE panels scannable: compact headings, short labels, fixed rows, and predictable alignment.

## Chinese Text

- Preserve UTF-8 files and read/write docs with explicit UTF-8 in PowerShell.
- Verify rendered Chinese after font/theme changes; mojibake is a release-blocking UI bug.
- Prefer font fallback registration at startup over per-widget font hacks.

## QA Expectations

- After visual changes, build the executable and capture by real HWND with DWM extended frame bounds plus `PrintWindow`.
- Verify custom title-bar dragging by comparing HWND rect before and after a real pointer drag.
- Store screenshots under `target/verification/` and record the screenshot path in the project commit cache.
