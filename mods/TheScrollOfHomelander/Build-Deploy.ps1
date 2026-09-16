param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu',
    [switch]$Deploy,
    [string]$DevelopmentRoot = (Join-Path $env:LOCALAPPDATA 'TaiwuStudio\ModDevelopment\TheScrollOfHomelander-quality'),
    [string]$BackupRoot = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'TaiwuModBackups')
)

$ErrorActionPreference = 'Stop'
$modRoot = $PSScriptRoot
$repoRoot = [IO.Path]::GetFullPath((Join-Path $modRoot '..\..'))
$worker = Join-Path $repoRoot 'tools\TaiwuStudio.RoslynWorker\bin\Release\net8.0\TaiwuStudio.RoslynWorker.exe'
$steamRoot = Join-Path $GameRoot 'Mod\TheScrollOfHomelander'
$records = Join-Path $modRoot 'build-records'
New-Item -ItemType Directory -Path $records -Force | Out-Null
$sides = @(
    @{ Name = 'Frontend'; Folder = 'Front'; Libs = 'The Scroll of Taiwu_Data\Managed' },
    @{ Name = 'Backend'; Folder = 'Back'; Libs = 'Backend' }
)

foreach ($side in $sides) {
    $assembly = 'BetterTaiwuScroll' + $side.Name
    $raw = & $worker build --project-root $modRoot --game-libs (Join-Path $GameRoot $side.Libs) `
        --assembly-name $assembly --output-plugin-path ($side.Folder + '/' + $assembly + '.dll') `
        --source-dir ('Scripts/' + $side.Name) --source-dir Scripts/Shared --configuration Release
    $exitCode = $LASTEXITCODE
    $raw | Set-Content -LiteralPath (Join-Path $records ($side.Name + '.json')) -Encoding UTF8
    $result = ($raw -join "`n") | ConvertFrom-Json
    $errors = @($result.diagnostics | Where-Object severity -eq 'error')
    if ($exitCode -ne 0 -or !$result.success -or $errors.Count -gt 0) {
        $errors | Format-List | Out-Host
        throw ($side.Name + ' compilation failed; nothing deployed.')
    }
    Write-Host ($side.Name + ': compiled; warnings=' + @($result.diagnostics | Where-Object severity -eq 'warning').Count)
}

function Get-RelativeHashes([string]$Root, [array]$Files) {
    @($Files | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}

function Get-SourceRevision([string]$Root) {
    try {
        $revision = (& git -C $Root rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -eq 0 -and $revision) { return ($revision | Select-Object -First 1).Trim() }
    } catch { }
    return 'unversioned'
}

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'Scripts') -Recurse -File)
$sourceFiles += @(Get-Item -LiteralPath (Join-Path $modRoot 'Build-Deploy.ps1'), (Join-Path $modRoot 'Config.lua'), (Join-Path $modRoot 'Settings.Lua'))
$sources = @(Get-RelativeHashes $modRoot $sourceFiles)
$sourceText = ($sources | ForEach-Object { $_.path + ':' + $_.sha256 }) -join "`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $sourceDigest = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sourceText))).Replace('-', '') }
finally { $sha.Dispose() }
$plugins = @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'Plugins') -Recurse -File | Where-Object Extension -in '.dll', '.pdb')
$gameFiles = @(
    Get-Item -LiteralPath (Join-Path $GameRoot 'The Scroll of Taiwu_Data\Managed\Assembly-CSharp.dll')
    Get-Item -LiteralPath (Join-Path $GameRoot 'The Scroll of Taiwu_Data\Managed\GameData.Shared.dll')
    Get-Item -LiteralPath (Join-Path $GameRoot 'Backend\GameData.dll')
    Get-Item -LiteralPath (Join-Path $GameRoot 'Backend\GameData.Shared.dll')
)
$manifest = [ordered]@{
    schemaVersion = 1
    createdUtc = [DateTime]::UtcNow.ToString('o')
    sourceRevision = (Get-SourceRevision $repoRoot)
    sourceDigest = $sourceDigest
    sourceFiles = $sources
    gameAssemblies = @(Get-RelativeHashes $GameRoot $gameFiles)
    plugins = @(Get-RelativeHashes $modRoot $plugins)
    validation = 'Independent compilation and static API inspection only. No game or automated tests run.'
}
$manifestPath = Join-Path $modRoot 'release-manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

if ($Deploy) {
    if (!(Test-Path -LiteralPath (Join-Path $steamRoot 'Config.lua'))) { throw 'Steam Mod destination is missing.' }
    $backup = Join-Path $BackupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-quality-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $steamRoot -Destination (Join-Path $backup 'TheScrollOfHomelander') -Recurse
    foreach ($destination in @($steamRoot, $DevelopmentRoot)) {
        $newDevelopmentCopy = !(Test-Path -LiteralPath $destination)
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        if ($newDevelopmentCopy) {
            foreach ($name in @('Config.lua', 'Settings.Lua', 'Assets', 'GradeBackgrounds', 'icon.png')) {
                $asset = Join-Path $modRoot $name
                if (Test-Path -LiteralPath $asset) { Copy-Item -LiteralPath $asset -Destination $destination -Recurse -Force }
            }
        }
        foreach ($file in $sourceFiles | Where-Object { $_.Extension -eq '.cs' }) {
            $relative = [IO.Path]::GetRelativePath($modRoot, $file.FullName)
            $target = Join-Path $destination $relative
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
        foreach ($file in $plugins) {
            $relative = [IO.Path]::GetRelativePath($modRoot, $file.FullName)
            $target = Join-Path $destination $relative
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            $temporary = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
            Copy-Item -LiteralPath $file.FullName -Destination $temporary
            if (Test-Path -LiteralPath $target) { [IO.File]::Replace($temporary, $target, [NullString]::Value) }
            else { [IO.File]::Move($temporary, $target) }
        }
        Copy-Item -LiteralPath $manifestPath -Destination $destination -Force
        foreach ($entry in @($manifest.plugins) + @($manifest.sourceFiles | Where-Object { $_.path -like 'Scripts/*' })) {
            $actual = (Get-FileHash -LiteralPath (Join-Path $destination $entry.path) -Algorithm SHA256).Hash
            if ($actual -ne $entry.sha256) { throw ('Deployment hash mismatch: ' + $entry.path) }
        }
        Write-Host ('Deployed and hash-matched: ' + $destination)
    }
    Write-Host ('Backup: ' + $backup)
}
Write-Host ('Source digest: ' + $sourceDigest)
