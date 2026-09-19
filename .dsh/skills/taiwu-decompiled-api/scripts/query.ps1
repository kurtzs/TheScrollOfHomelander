#!/usr/bin/env pwsh
# Taiwu decompiled API query wrapper.
#
# Thin, readable wrapper around tools\TaiwuStudio.DecompilerWorker.exe. It reads the
# currently installed game assemblies directly, so there is no external index or cache
# to refresh and every answer matches the game version that is actually installed.
#
#   .\query.ps1 find-type   CharacterDomain -Assembly GameData -Limit 20
#   .\query.ps1 find-member GetTaiwuCharId  -Assembly GameData -Limit 20
#   .\query.ps1 type        GameData.Domains.Taiwu.TaiwuDomain -Assembly GameData
#   .\query.ps1 member      GameData.Domains.Taiwu.TaiwuDomain GetTaiwuCharId -Assembly GameData
#   .\query.ps1 member      -Token 0x0600589E -Assembly GameData
#
# By default `type`/`member` print only the declared span (plus -Context lines), because
# a decompiled type can be tens of thousands of lines. Use -Full to print everything, or
# -Json to get the worker envelope for scripting.
#
# -Assembly selects the side: GameData* lives in the Backend directory, Assembly-CSharp*
# in The Scroll of Taiwu_Data\Managed. When omitted, both directories are searched.

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('find-type', 'find-member', 'type', 'member')]
    [string]$Command,

    [Parameter(Position = 1)][string]$Query,
    [Parameter(Position = 2)][string]$Name,
    [string]$Assembly,
    [string]$Token,
    [int]$Limit = 50,
    [int]$Context = 20,
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu',
    [string]$Worker,
    [switch]$Full,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

# Locate the workspace root by walking up until the worker project appears, so this
# script works both from the installed skill bundle and from a repository checkout.
$repoRoot = $null
$probe = $PSScriptRoot
for ($i = 0; $i -lt 6 -and $probe; $i++) {
    if (Test-Path -LiteralPath (Join-Path $probe 'tools\TaiwuStudio.DecompilerWorker\TaiwuStudio.DecompilerWorker.csproj')) {
        $repoRoot = $probe
        break
    }
    $probe = Split-Path -Parent $probe
}
if (-not $repoRoot) { $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }

if (-not $Worker) { $Worker = $env:TAIWU_DECOMPILER_WORKER }
if (-not $Worker) {
    $Worker = Join-Path $repoRoot 'tools\TaiwuStudio.DecompilerWorker\bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe'
}

$frontendDir = Join-Path $GameRoot 'The Scroll of Taiwu_Data\Managed'
$backendDir = Join-Path $GameRoot 'Backend'

function Get-WorkerPath {
    if (Test-Path -LiteralPath $Worker) { return $Worker }
    $debug = Join-Path $repoRoot 'tools\TaiwuStudio.DecompilerWorker\bin\Debug\net8.0\TaiwuStudio.DecompilerWorker.exe'
    if (Test-Path -LiteralPath $debug) { return $debug }
    throw "Decompiler worker not found. Build it with:`n  dotnet build `"$repoRoot\tools\TaiwuStudio.DecompilerWorker\TaiwuStudio.DecompilerWorker.csproj`" -c Release"
}

function Resolve-ManagedDir([string]$AssemblyName) {
    foreach ($dir in @($backendDir, $frontendDir)) {
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        if (-not $AssemblyName) {
            # Without -Assembly the worker searches every assembly in the directory;
            # prefer the backend directory because it also contains GameData.Shared.dll.
            return $dir
        }
        foreach ($suffix in @('', '.dll', '.exe')) {
            if (Test-Path -LiteralPath (Join-Path $dir ($AssemblyName + $suffix))) { return $dir }
        }
    }
    if ($AssemblyName) {
        throw "Assembly '$AssemblyName' was not found in:`n  $backendDir`n  $frontendDir"
    }
    throw "Neither game assembly directory exists under: $GameRoot"
}

function Invoke-Worker([string[]]$WorkerArgs) {
    $workerPath = Get-WorkerPath
    $text = ((& $workerPath @WorkerArgs 2>&1) | Out-String).Trim()
    try { $envelope = $text | ConvertFrom-Json } catch { $envelope = $null }
    if ($null -eq $envelope) { throw "Worker returned unparsable output: $text" }
    # Worker envelope: { ok, payload, diagnostics }. Diagnostics travel with the envelope,
    # so re-emit them on the payload for the callers that only see the payload. The raw
    # text is carried along so -Json can print it without re-running the worker.
    if ($null -ne $envelope.payload) {
        $envelope.payload | Add-Member -NotePropertyName diagnostics -NotePropertyValue $envelope.diagnostics -Force
        $envelope.payload | Add-Member -NotePropertyName rawJson -NotePropertyValue $text -Force
        $envelope.payload | Add-Member -NotePropertyName ok -NotePropertyValue $envelope.ok -Force
    }
    if (-not $envelope.ok) {
        $message = ($envelope.diagnostics | ForEach-Object { $_.message }) -join '; '
        throw "Worker failed: $message"
    }
    foreach ($diagnostic in @($envelope.diagnostics)) {
        if ($diagnostic.severity -eq 'warning' -and -not $script:quietWarnings) { Write-Warning $diagnostic.message }
    }
    $envelope.payload
}

function Get-SuggestedAssembly([string]$Message) {
    # Catch: "... found, but only in GameData.Shared" when -Assembly names the wrong assembly
    # of the same side. Return that assembly name so the caller can retry once.
    $match = [regex]::Match($Message, 'but only in ([A-Za-z0-9_.]+)')
    if ($match.Success) { return $match.Groups[1].Value }
    return $null
}

function Write-Source($Payload) {
    $lines = ($Payload.source -replace "`r`n", "`n").Split("`n")
    if ($Full -or $null -eq $Payload.sourceSpan) {
        $lines
        return
    }
    $start = [Math]::Max(1, $Payload.sourceSpan.startLine - $Context)
    $end = [Math]::Min($lines.Count, $Payload.sourceSpan.endLine + $Context)
    "[source is $($lines.Count) lines; showing $start-$end, use -Full for the whole type]"
    for ($i = $start; $i -le $end; $i++) {
        '{0,6}: {1}' -f $i, $lines[$i - 1]
    }
}

switch ($Command) {
    'find-type' {
        if (-not $Query) { throw 'find-type needs a query, e.g.: query.ps1 find-type CharacterDomain -Assembly GameData' }
        $managed = Resolve-ManagedDir $Assembly
        $workerArgs = @('--command', 'find-type', '--managed-dir', $managed, '--query', $Query, '--limit', $Limit)
        if ($Assembly) { $workerArgs += @('--assembly', $Assembly) }
        $payload = Invoke-Worker $workerArgs
        if ($Json) { $payload.rawJson } else {
        "managedDir: $managed"
        "query: $Query   matches: $($payload.totalMatches) (showing $($payload.results.Count), limit $($payload.limit))"
        foreach ($row in $payload.results) {
            '{0}::{1}  {2}  {3}' -f $row.assembly, $row.fullName, $row.token, $row.assemblyFileName
        }
        }
    }
    'find-member' {
        if (-not $Query) { throw 'find-member needs a query, e.g.: query.ps1 find-member GetTaiwuCharId -Assembly GameData' }
        $managed = Resolve-ManagedDir $Assembly
        $workerArgs = @('--command', 'find-member', '--managed-dir', $managed, '--query', $Query, '--limit', $Limit)
        if ($Assembly) { $workerArgs += @('--assembly', $Assembly) }
        $payload = Invoke-Worker $workerArgs
        if ($Json) { $payload.rawJson } else {
        "managedDir: $managed"
        "query: $Query   matches: $($payload.totalMatches) (showing $($payload.results.Count), limit $($payload.limit))"
        foreach ($row in $payload.results) {
            '{0}::{1}.{2}  [{3}]  {4}  {5}' -f $row.assembly, $row.typeFullName, $row.name, $row.kind, $row.token, $row.signature
        }
        if ($payload.totalMatches -gt $payload.results.Count) {
            "note: only the first $($payload.limit) matches are shown; raise -Limit for the rest"
        }
        }
    }
    'type' {
        if (-not $Query) { throw 'type needs the full type name, e.g.: query.ps1 type GameData.Domains.Taiwu.TaiwuDomain -Assembly GameData' }
        if (-not $Assembly) { throw 'type needs -Assembly so the right game side is read, e.g. -Assembly GameData' }
        $managed = Resolve-ManagedDir $Assembly
        $typeArgs = @('--command', 'decompile-type', '--managed-dir', $managed, '--assembly', $Assembly, '--type', $Query)
        try {
            $payload = Invoke-Worker $typeArgs
        } catch {
            # The type may live in a sibling assembly of the same side (GameData vs
            # GameData.Shared); the worker tells us where, so retry once with that name.
            $suggested = Get-SuggestedAssembly $_.Exception.Message
            if (-not $suggested) { throw }
            $typeArgs = @('--command', 'decompile-type', '--managed-dir', $managed, '--assembly', $suggested, '--type', $Query)
            $payload = Invoke-Worker $typeArgs
        }
        if ($Json) { $payload.rawJson } else {
        "assembly: $($payload.assembly)   type: $($payload.typeFullName)"
        if ($payload.sourceSpan) { "span: $($payload.sourceSpan.startLine)-$($payload.sourceSpan.endLine)" }
        Write-Source $payload
        }
    }
    'member' {
        if (-not $Token -and -not ($Query -and $Name)) {
            throw 'member needs -Token 0x... or a full type plus member name, e.g.: query.ps1 member GameData.Domains.Taiwu.TaiwuDomain GetTaiwuCharId -Assembly GameData'
        }
        if (-not $Assembly) { throw 'member needs -Assembly so the right game side is read, e.g. -Assembly GameData' }
        $managed = Resolve-ManagedDir $Assembly
        $workerArgs = @('--command', 'decompile-member', '--managed-dir', $managed, '--assembly', $Assembly)
        if ($Token) {
            $workerArgs += @('--token', $Token)
        } else {
            $workerArgs += @('--type', $Query, '--member', $Name)
        }
        try {
            $payload = Invoke-Worker $workerArgs
        } catch {
            # Retry once with the corrected spellings a decompiler needs, then report the
            # original worker error. Guessing further (scanning other assemblies) hides the
            # real problem, which is almost always a wrong -Assembly or a wrong member name.
            $failed = $_.Exception.Message
            $payload = $null
            $resolvedAssembly = $Assembly
            # Shared contracts live in GameData.Shared while domain logic lives in GameData,
            # so a GameData lookup of a shared type is the common miss. The worker also names
            # the owning assembly for top-level moves; try that first when it does.
            $candidates = New-Object System.Collections.Generic.List[string]
            $candidates.Add((Get-SuggestedAssembly $failed))
            if ($Assembly -like 'GameData*' -and $Assembly -ne 'GameData.Shared') { $candidates.Add('GameData.Shared') }
            if ($Assembly -eq 'GameData.Shared') { $candidates.Add('GameData') }
            foreach ($candidateAssembly in $candidates) {
                if (-not $candidateAssembly -or $candidateAssembly -eq $resolvedAssembly) { continue }
                $retryArgs = @('--command', 'decompile-member', '--managed-dir', $managed, '--assembly', $candidateAssembly)
                if ($Token) { $retryArgs += @('--token', $Token) } else { $retryArgs += @('--type', $Query, '--member', $Name) }
                try { $payload = Invoke-Worker $retryArgs; $resolvedAssembly = $candidateAssembly; break } catch { $payload = $null }
            }
            if (-not $payload -and -not $Token -and $Name -notlike 'get_*' -and $Name -notlike 'set_*' -and $Name -notlike 'op_*') {
                # Property accessors and operators are compiled as get_X/set_X/op_X.
                foreach ($accessor in @('get_' + $Name, 'set_' + $Name)) {
                    $retryArgs = @('--command', 'decompile-member', '--managed-dir', $managed, '--assembly', $resolvedAssembly, '--type', $Query, '--member', $accessor)
                    try { $payload = Invoke-Worker $retryArgs; break } catch { $payload = $null }
                }
            }
            if (-not $payload) {
                throw ("$failed`nHint: confirm the declaring assembly with find-member, or query the type with the 'type' command to see its real members.")
            }
        }
        if ($Json) { $payload.rawJson } else {
        "assembly: $($payload.assembly)   type: $($payload.typeFullName)"
        "member: $($payload.memberName)   token: $($payload.token)"
        if ($payload.sourceSpan) { "span: $($payload.sourceSpan.startLine)-$($payload.sourceSpan.endLine)" }
        Write-Source $payload
        }
    }
}
