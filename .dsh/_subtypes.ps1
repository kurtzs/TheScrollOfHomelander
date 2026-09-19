$root='C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu\The Scroll of Taiwu_Data\StreamingAssets'
$m = Get-Content "$root\ConfigRefNameMapping\MakeItemSubType.ref.txt"
Write-Output '=== every MakeItemSubType entry 180..313 ==='
for ($i=1; $i -lt $m.Count; $i+=2) {
  $id=[int]$m[$i]
  if ($id -ge 180) { "id=$id`t$($m[$i-1])" }
}
