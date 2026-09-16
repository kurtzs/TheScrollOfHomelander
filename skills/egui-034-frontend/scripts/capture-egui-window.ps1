param(
    [string]$ExePath,
    [string]$ProcessName,
    [string]$WindowTitle = "Taiwu Studio",
    [Parameter(Mandatory = $false)]
    [string]$OutPath = "target\verification\egui-window-hwnd.png",
    [switch]$Launch,
    [switch]$VerifyDrag,
    [int]$DragStartX = 220,
    [int]$DragStartY = 24,
    [int]$DragDx = 84,
    [int]$DragDy = 52,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class EguiHwndCaptureNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    public static string GetTitle(IntPtr hwnd)
    {
        StringBuilder builder = new StringBuilder(512);
        GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }
}
"@

if ($ValidateOnly) {
    [pscustomobject]@{
        ok = $true
        script = "capture-egui-window.ps1"
    } | ConvertTo-Json -Compress
    exit 0
}

if ($Launch) {
    if ([string]::IsNullOrWhiteSpace($ExePath)) {
        throw "-ExePath is required with -Launch."
    }
    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "Executable not found: $ExePath"
    }
    $process = Start-Process -FilePath $ExePath -PassThru
    if ([string]::IsNullOrWhiteSpace($ProcessName)) {
        $ProcessName = [System.IO.Path]::GetFileNameWithoutExtension($ExePath)
    }
    Start-Sleep -Milliseconds 900
} elseif ([string]::IsNullOrWhiteSpace($ProcessName) -and -not [string]::IsNullOrWhiteSpace($ExePath)) {
    $ProcessName = [System.IO.Path]::GetFileNameWithoutExtension($ExePath)
}

if ([string]::IsNullOrWhiteSpace($ProcessName)) {
    throw "Provide -ProcessName or -ExePath."
}

function Convert-Rect {
    param($Rect)
    [pscustomobject]@{
        left = $Rect.Left
        top = $Rect.Top
        right = $Rect.Right
        bottom = $Rect.Bottom
        width = $Rect.Right - $Rect.Left
        height = $Rect.Bottom - $Rect.Top
    }
}

function Get-WindowRect {
    param([IntPtr]$Hwnd)
    $rect = New-Object EguiHwndCaptureNative+RECT
    if (-not [EguiHwndCaptureNative]::GetWindowRect($Hwnd, [ref]$rect)) {
        throw "GetWindowRect failed."
    }
    $rect
}

function Get-DwmFrameRect {
    param([IntPtr]$Hwnd)
    $rect = New-Object EguiHwndCaptureNative+RECT
    $result = [EguiHwndCaptureNative]::DwmGetWindowAttribute(
        $Hwnd,
        9,
        [ref]$rect,
        [Runtime.InteropServices.Marshal]::SizeOf([type]"EguiHwndCaptureNative+RECT")
    )
    if ($result -ne 0 -or ($rect.Right -le $rect.Left) -or ($rect.Bottom -le $rect.Top)) {
        $rect = Get-WindowRect -Hwnd $Hwnd
    }
    $rect
}

function Find-TargetWindow {
    $matches = New-Object System.Collections.Generic.List[object]
    $callback = [EguiHwndCaptureNative+EnumWindowsProc]{
        param([IntPtr]$hwnd, [IntPtr]$lparam)

        if (-not [EguiHwndCaptureNative]::IsWindowVisible($hwnd)) {
            return $true
        }

        $windowProcessId = [uint32]0
        [void][EguiHwndCaptureNative]::GetWindowThreadProcessId($hwnd, [ref]$windowProcessId)
        $proc = Get-Process -Id $windowProcessId -ErrorAction SilentlyContinue
        if ($null -eq $proc -or $proc.ProcessName -ne $ProcessName) {
            return $true
        }

        $title = [EguiHwndCaptureNative]::GetTitle($hwnd)
        if (-not [string]::IsNullOrWhiteSpace($WindowTitle) -and $title -notlike "*$WindowTitle*") {
            return $true
        }

        $frame = Get-DwmFrameRect -Hwnd $hwnd
        $width = $frame.Right - $frame.Left
        $height = $frame.Bottom - $frame.Top
        if ($width -lt 320 -or $height -lt 200) {
            return $true
        }

        $matches.Add([pscustomobject]@{
            hwnd = $hwnd
            process_id = $windowProcessId
            title = $title
            frame = $frame
            area = $width * $height
        }) | Out-Null

        return $true
    }

    [void][EguiHwndCaptureNative]::EnumWindows($callback, [IntPtr]::Zero)
    $matches | Sort-Object -Property area -Descending | Select-Object -First 1
}

$deadline = (Get-Date).AddSeconds(12)
$target = $null
while ($null -eq $target -and (Get-Date) -lt $deadline) {
    $target = Find-TargetWindow
    if ($null -eq $target) {
        Start-Sleep -Milliseconds 250
    }
}

if ($null -eq $target) {
    throw "No visible HWND matched process '$ProcessName' and title '$WindowTitle'."
}

$hwnd = [IntPtr]$target.hwnd
$frameRect = Get-DwmFrameRect -Hwnd $hwnd
$frame = Convert-Rect $frameRect
if ($frame.width -lt 320 -or $frame.height -lt 200) {
    throw "Implausible DWM frame size: $($frame.width)x$($frame.height)."
}

$outFullPath = [System.IO.Path]::GetFullPath($OutPath)
$outDir = Split-Path -Parent $outFullPath
if (-not [string]::IsNullOrWhiteSpace($outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$screenDc = [EguiHwndCaptureNative]::GetDC([IntPtr]::Zero)
$memDc = [EguiHwndCaptureNative]::CreateCompatibleDC($screenDc)
$bitmapHandle = [EguiHwndCaptureNative]::CreateCompatibleBitmap($screenDc, $frame.width, $frame.height)
$oldObject = [EguiHwndCaptureNative]::SelectObject($memDc, $bitmapHandle)
$printOk = $false

try {
    $printOk = [EguiHwndCaptureNative]::PrintWindow($hwnd, $memDc, 2)
    if (-not $printOk) {
        throw "PrintWindow failed for HWND $hwnd."
    }
    $bitmap = [System.Drawing.Image]::FromHbitmap($bitmapHandle)
    try {
        $bitmap.Save($outFullPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
    }
} finally {
    [void][EguiHwndCaptureNative]::SelectObject($memDc, $oldObject)
    [void][EguiHwndCaptureNative]::DeleteObject($bitmapHandle)
    [void][EguiHwndCaptureNative]::DeleteDC($memDc)
    [void][EguiHwndCaptureNative]::ReleaseDC([IntPtr]::Zero, $screenDc)
}

$dragResult = $null
if ($VerifyDrag) {
    $beforeRect = Get-WindowRect -Hwnd $hwnd
    [void][EguiHwndCaptureNative]::ShowWindow($hwnd, 9)
    [void][EguiHwndCaptureNative]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 120

    $startX = $beforeRect.Left + $DragStartX
    $startY = $beforeRect.Top + $DragStartY
    [void][EguiHwndCaptureNative]::SetCursorPos($startX, $startY)
    Start-Sleep -Milliseconds 80
    [EguiHwndCaptureNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    $steps = 8
    for ($i = 1; $i -le $steps; $i++) {
        $nextX = [int]($startX + ($DragDx * $i / $steps))
        $nextY = [int]($startY + ($DragDy * $i / $steps))
        [void][EguiHwndCaptureNative]::SetCursorPos($nextX, $nextY)
        [EguiHwndCaptureNative]::mouse_event(0x0001, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 45
    }
    [void][EguiHwndCaptureNative]::SetCursorPos($startX + $DragDx, $startY + $DragDy)
    Start-Sleep -Milliseconds 120
    [EguiHwndCaptureNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 220

    $afterRect = Get-WindowRect -Hwnd $hwnd
    $deltaX = $afterRect.Left - $beforeRect.Left
    $deltaY = $afterRect.Top - $beforeRect.Top
    $dragResult = [pscustomobject]@{
        before = Convert-Rect $beforeRect
        after = Convert-Rect $afterRect
        delta_x = $deltaX
        delta_y = $deltaY
        expected_x = $DragDx
        expected_y = $DragDy
        moved = [Math]::Abs($deltaX) -gt 8 -or [Math]::Abs($deltaY) -gt 8
    }

    if (-not $dragResult.moved) {
        throw "Drag verification failed: window did not move."
    }
}

[pscustomobject]@{
    ok = $true
    hwnd = ("0x{0:X}" -f $hwnd.ToInt64())
    process_name = $ProcessName
    title = $target.title
    frame = $frame
    print_window_ok = $printOk
    output = $outFullPath
    drag = $dragResult
} | ConvertTo-Json -Depth 8
