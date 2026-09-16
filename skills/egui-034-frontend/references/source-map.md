# egui 0.34 Source Map

Use the local clone first:

`C:\Users\Administrator\Documents\GitHub\deltawater\egui`

## Common Lookups

- Viewport commands and `ViewportBuilder`: `crates/egui/src/viewport.rs`
- Command dispatch through winit: `crates/egui-winit/src/lib.rs`
- Native eframe integration: `crates/eframe/src/native/`
- App trait and lifecycle: `crates/eframe/src/epi.rs`
- Core style and visuals: `crates/egui/src/style.rs`
- Fonts and text layout: `crates/egui/src/text/`, `crates/egui/src/font_selection.rs`
- Layout and child UI allocation: `crates/egui/src/ui.rs`
- Frames, panels, windows, menus, resize: `crates/egui/src/containers/`
- Buttons, text edit, labels, progress bars: `crates/egui/src/widgets/`
- Demo examples for idioms: `crates/egui_demo_lib/src/`, `examples/`

## Search Patterns

Run these before guessing an API:

```powershell
rg -n "ViewportCommand|StartDrag|Decorations|InnerSize|OuterPosition" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates"
rg -n "Visuals|WidgetVisuals|Style|Spacing|CornerRadius|Stroke" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates\egui\src"
rg -n "TextEdit|Layouter|FontDefinitions|FontData|FontFamily" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates\egui\src"
rg -n "Response|Sense|interact|allocate_exact_size|allocate_ui_at_rect" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates\egui\src"
```

## Version Discipline

- Keep app dependencies at `egui = "0.34.3"` and `eframe = "0.34.3"`.
- If the lockfile keeps an older transitive pair, run `cargo update -p egui -p eframe`.
- Check `Cargo.lock` for a single coherent egui/eframe family after updates.

## 0.34 Practices

- Use `ctx.send_viewport_cmd(egui::ViewportCommand::...)` or `ui.ctx().send_viewport_cmd(...)` for viewport actions.
- Use `ViewportCommand::StartDrag` immediately on a primary press in a true drag region.
- Prefer `Visuals`, `Style`, `Spacing`, helper widgets, and theme functions over one-off inline colors.
- Give shell regions stable dimensions: title bar, activity bar, side bar, bottom status, tool rows, icon buttons, tab strips, and dense table rows.
- For editor-like UIs, prefer predictable panel geometry and restrained hover states over decorative card layouts.
