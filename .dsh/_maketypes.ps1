$q='C:\Users\Administrator\Documents\TheScrollOfHomelander-Workspace\.dsh\skills\taiwu-decompiled-api\scripts\query.ps1'
$raw = & $q type Config.MakeItemType -Assembly GameData.Shared -Full -Json
$src = ($raw | ConvertFrom-Json).payload.source
$rows = @()
foreach ($line in ($src -split "`n")) {
  if ($line -match 'new MakeItemTypeItem\((-?\d+),\s*LocalStringManager\.GetConfig\("MakeItemType_language",\s*"Name_(\d+)"\),\s*(-?\d+),.*?new List<short>\s*\{([^}]*)\}') {
    $rows += [pscustomobject]@{
      Tpl   = [int]$Matches[1]
      Name  = $Matches[2]
      Sub   = [int]$Matches[3]
      List  = $Matches[4].Trim()
      Count = ($Matches[4] -split ',').Count
    }
  }
}
"parsed: $($rows.Count)"
Write-Output '=== distinct ItemSubType values of make types with Tpl >= 120 ==='
$rows | Where-Object { $_.Tpl -ge 120 } | Group-Object Sub | Sort-Object { [int]$_.Name } | ForEach-Object {
  "itemSubType=$($_.Name)  types=$($_.Count)  sampleTpls=$(($_.Group | Select-Object -First 6 | ForEach-Object { $_.Tpl }) -join ',')"
}
Write-Output '=== make types whose subtype list contains 192 (百花锦蛇) ==='
$rows | Where-Object { ($_.List -split ',') -contains '192' } | ForEach-Object { "tpl=$($_.Tpl) itemSubType=$($_.Sub) list=[$($_.List)]" }
Write-Output '=== make types with itemSubType 700 / 701 / 799 ==='
$rows | Where-Object { $_.Sub -eq 700 -or $_.Sub -eq 701 -or $_.Sub -eq 799 } | ForEach-Object { "tpl=$($_.Tpl) itemSubType=$($_.Sub) list=[$($_.List)]" }
