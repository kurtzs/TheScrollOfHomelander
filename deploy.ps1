# Builds BetterTaiwuScroll (frontend + backend) by invoking the Roslyn compiler
# directly against the game's managed assemblies -- the same way Taiwu Studio's
# roslyn-worker does -- then assembles the mod folder and copies it into the
# game's local Mod directory for testing. No Taiwu Studio or .csproj required.
#
# The two halves target different runtimes and different assembly sets:
#   * frontend -> the Unity client, references <Game>\...\_Data\Managed
#   * backend  -> the separate .NET server process, references <Game>\Backend
#
#   .\deploy.ps1                     # build + deploy to the game's Mod folder
#   .\deploy.ps1 -SkipDeploy         # build only (into .\build), don't touch the game
param(
    [string]$GameDir = "C:\Steam\steamapps\common\The Scroll Of Taiwu",
    [string]$ModName = "BetterTaiwuScroll",
    [switch]$SkipDeploy
)
$ErrorActionPreference = "Stop"
$root       = $PSScriptRoot
$feManaged  = Join-Path $GameDir "The Scroll of Taiwu_Data\Managed"
$beManaged  = Join-Path $GameDir "Backend"
$build      = Join-Path $root "build"

foreach ($d in @($feManaged, $beManaged)) {
    if (-not (Test-Path $d)) { throw "Assembly directory not found: $d (check -GameDir)." }
}

# Locate the Roslyn C# compiler shipped with the .NET SDK.
$csc = Get-ChildItem "C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $csc) { throw "Could not find Roslyn csc.dll under the .NET SDK." }

# /reference lines for every *managed* assembly in a folder (native DLLs like the
# Steam stub or coreclr are skipped because GetAssemblyName throws on them).
function Get-ManagedRefs([string]$dir) {
    Get-ChildItem $dir -Filter *.dll | ForEach-Object {
        try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); "/reference:`"$($_.FullName)`"" }
        catch { }
    }
}

if (Test-Path $build) { Remove-Item $build -Recurse -Force }
New-Item -ItemType Directory -Path $build -Force | Out-Null

# The basic_harmony template injects these implicit global usings for the frontend;
# its .cs files rely on them. The backend uses explicit usings and needs none.
$globalUsings = Join-Path $build "GlobalUsings.g.cs"
@'
global using FrameWork.UISystem.UIElements;
global using GameData.Domains.Item.Display;
global using GameData.Domains.Character;
global using GameData.Domains.Taiwu;
global using Game.Views.Make;
global using Game.Views.Building;
global using Game.Components.ListStyleGeneralScroll;
global using Game.Components.Common;
'@ | Set-Content -Path $globalUsings -Encoding utf8

function Invoke-Csc([string]$name, [string]$srcDir, [string]$outDll, [string[]]$refs, [string[]]$extraSources) {
    $sources = Get-ChildItem (Join-Path $root $srcDir) -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
    $sources += $extraSources
    $rsp = Join-Path $build "$name.rsp"
    $rspArgs = @(
        "-nostdlib", "-nologo", "-target:library", "-unsafe",
        "-langversion:latest", "-debug:portable",
        "-nowarn:CS0618,CS0612,CS0414,CS0169,CS0649",
        "-out:`"$outDll`""
    ) + $refs + $sources
    Set-Content -Path $rsp -Value $rspArgs -Encoding utf8
    Write-Host "Building $name..." -ForegroundColor Cyan
    & dotnet exec "$csc" -noconfig "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw "$name build failed." }
}

$feDll = Join-Path $build "BetterTaiwuScrollFrontend.dll"
$beDll = Join-Path $build "BetterTaiwuScrollBackend.dll"
Invoke-Csc "frontend" "Scripts\Frontend" $feDll (Get-ManagedRefs $feManaged) @("`"$globalUsings`"")
Invoke-Csc "backend"  "Scripts\Backend"  $beDll (Get-ManagedRefs $beManaged) @()

if ($SkipDeploy) {
    Write-Host "Built (no deploy):`n  $feDll`n  $beDll" -ForegroundColor Green
    return
}

# Assemble the mod folder that mirrors Config.lua's plugin layout and copy it in.
$dest = Join-Path $GameDir "Mod\$ModName"
Write-Host "Packaging into $dest" -ForegroundColor Cyan
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $dest "Plugins\Front") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dest "Plugins\Back")  -Force | Out-Null

Copy-Item (Join-Path $root "Config.lua")   $dest
Copy-Item (Join-Path $root "Settings.Lua") $dest
Copy-Item (Join-Path $root "icon.png")     $dest
if (Test-Path (Join-Path $root "UserData")) {
    Copy-Item (Join-Path $root "UserData") $dest -Recurse
}
Copy-Item $feDll                                       (Join-Path $dest "Plugins\Front")
Copy-Item ([IO.Path]::ChangeExtension($feDll, ".pdb")) (Join-Path $dest "Plugins\Front")
Copy-Item $beDll                                       (Join-Path $dest "Plugins\Back")
Copy-Item ([IO.Path]::ChangeExtension($beDll, ".pdb")) (Join-Path $dest "Plugins\Back")

Write-Host "Deployed:" -ForegroundColor Green
Get-ChildItem $dest -Recurse -File | ForEach-Object { "  " + $_.FullName.Substring($dest.Length + 1) }
