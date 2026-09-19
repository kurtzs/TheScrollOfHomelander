$root='C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\The Scroll of Taiwu_Data\StreamingAssets'
$f = Get-Content "$root\ConfigRefNameMapping\Food.ref.txt" -Encoding UTF8
$pairs = @()
for ($i=1; $i -lt $f.Count; $i+=2) {
  if ($f[$i] -match '^-?\d+$') { $pairs += [pscustomobject]@{ id=[int]$f[$i]; name=$f[$i-1] } }
}
"pairs: $($pairs.Count)  min=$($pairs[0].id)  max=$(($pairs | Measure-Object id -Maximum).Maximum)"
Write-Output '=== ids 28..44 (鸭/鹅 dishes?) ==='
$pairs | Where-Object { $_.id -ge 28 -and $_.id -le 44 } | ForEach-Object { "id=$($_.id)`t$($_.name)" }
Write-Output '=== ids 70..80 ==='
$pairs | Where-Object { $_.id -ge 70 -and $_.id -le 80 } | ForEach-Object { "id=$($_.id)`t$($_.name)" }
Write-Output '=== everything after 152 (non-ingredient?) ==='
$pairs | Where-Object { $_.id -gt 152 } | ForEach-Object { "id=$($_.id)`t$($_.name)" }
