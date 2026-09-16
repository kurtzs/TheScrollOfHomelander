# egui Window Verification on Windows

Use this flow for final UI screenshots:

1. Start the app from the built executable.
2. Enumerate visible top-level windows for the process.
3. Select the window titled `Taiwu Studio` or the largest sane visible app window.
4. Use `DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, ...)` for physical bitmap dimensions.
5. Call `PrintWindow(hwnd, hdc, 2)` into a bitmap.
6. Save under `target/verification/`.

For custom title-bar dragging:

1. Record `GetWindowRect(hwnd)` before.
2. Send foreground focus.
3. Move cursor to a known drag-region coordinate.
4. Send left down, move, left up.
5. Record `GetWindowRect(hwnd)` after.
6. Pass only when the window rect moves by the expected delta.

Do not accept a final screenshot produced by plain `CopyFromScreen` over a guessed rectangle.

## Reusable Script

When the project has no stronger local capture helper, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File "C:\Users\Administrator\.codex\skills\egui-034-frontend\scripts\capture-egui-window.ps1" `
  -ExePath "target\debug\taiwu_studio.exe" `
  -WindowTitle "Taiwu Studio" `
  -OutPath "target\verification\stage-name-hwnd.png" `
  -Launch `
  -VerifyDrag
```

The script uses visible top-level windows, process IDs, DWM extended frame bounds, `PrintWindow(hwnd, hdc, 2)`, and optional Win32 mouse input for title-bar drag verification.

## Failure Rules

- If `PrintWindow` returns false, treat the screenshot as invalid.
- If the bitmap dimensions are implausibly small, choose another matching HWND or fail the verification.
- If drag delta is zero after a title-bar change, fix the drag region before continuing.
- If the screenshot is clipped, recapture from DWM frame bounds; do not move the window onto screen and crop manually.
