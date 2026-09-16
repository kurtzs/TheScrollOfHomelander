# egui 0.34 Migration Notes

Use this reference when a project is moving to or compiling against `egui = "0.34.3"` and `eframe = "0.34.3"`.

## Dependency Alignment

- Keep `egui`, `eframe`, and direct `egui_extras` dependencies on the same 0.34 family.
- If `Cargo.lock` keeps older packages, run `cargo update -p egui -p eframe` and include any direct egui-family crates that remain stale.
- Verify `Cargo.lock` does not contain multiple egui versions unless the project intentionally depends on an older third-party crate.

## Native App Entry

- `eframe::run_native` app creators return `Result<Box<dyn App>, eframe::Error>`:

```rust
eframe::run_native(
    "Taiwu Studio",
    native_options,
    Box::new(|cc| Ok(Box::new(TaiwuStudioApp::new(cc)))),
)
```

- Configure the main window through `eframe::NativeOptions { viewport: egui::ViewportBuilder::default()... }`.
- For custom chrome, use `ViewportBuilder::with_decorations(false)` and keep drag handling inside the title bar implementation.

## Viewport Commands

- Send commands through `ctx.send_viewport_cmd(...)` or `ui.ctx().send_viewport_cmd(...)`.
- Use `egui::ViewportCommand::StartDrag` immediately after primary press in a true drag region.
- Use `egui::ViewportCommand::Minimized(true)`, `Maximized(bool)`, and `Close` for title-bar buttons.
- Do not place minimize, maximize, close, menu, tab, or splitter widgets inside the drag region.

## Renames And Signature Changes

- Prefer `egui::CornerRadius` over deprecated `egui::Rounding`.
- Prefer `.corner_radius(...)` over deprecated `.rounding(...)` on frames, buttons, images, and progress bars.
- `Painter::rect_stroke` takes a `StrokeKind` argument:

```rust
ui.painter().rect_stroke(
    rect,
    egui::CornerRadius::same(4),
    egui::Stroke::new(1.0, color),
    egui::StrokeKind::Inside,
);
```

- Check `crates/egui/src/lib.rs`, `crates/egui/src/painter.rs`, and `crates/egui/src/style.rs` in the local clone before relying on old examples.

## Fonts And Chinese UI

- Register fonts once at startup through `egui::FontDefinitions` and `ctx.set_fonts(...)`.
- Use `egui::FontData::from_static(...)` for embedded bytes and `egui::FontData::from_owned(...)` for loaded bytes.
- Insert Chinese-capable fonts into both `FontFamily::Proportional` and `FontFamily::Monospace` when editor text and labels both need fallback.
- Treat mojibake as an encoding bug, not a font bug: preserve UTF-8 source files and avoid lossy PowerShell reads/writes.

## Theme And Widgets

- Centralize colors in a theme function that edits `Style`, `Visuals`, `Spacing`, and widget visuals.
- Buttons should expose visible normal, hovered, active, focused, disabled, and selected states.
- Use stable dimensions for title bars, sidebars, tool buttons, tabs, status bars, tree rows, and diagnostics rows so hover state cannot resize the layout.

## Local Source Checks

Useful searches:

```powershell
rg -n "pub enum ViewportCommand|StartDrag|Minimized|Maximized|Close" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates\egui\src\viewport.rs"
rg -n "pub fn rect_stroke|StrokeKind" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates\egui\src\painter.rs"
rg -n "CornerRadius|type Rounding" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates\egui\src"
rg -n "FontData|from_static|from_owned|FontDefinitions" "C:\Users\Administrator\Documents\GitHub\deltawater\egui\crates"
```
