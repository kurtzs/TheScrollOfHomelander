# API 查询与构建

先完成 [环境检查](environment-and-tools.md)。命令从工作区根目录运行。

## 查询当前程序集

```powershell
$query = './skills/taiwu-decompiled-api/scripts/query.ps1'
& $query find-type UI_ -Assembly Assembly-CSharp -GameRoot $gameRoot -Limit 5
& $query find-member GetTaiwuCharId -Assembly GameData -GameRoot $gameRoot -Limit 20
```

根据本次输出选择完整类型、重载和 token，再调用 `member -Token $token -Assembly GameData -GameRoot $gameRoot`，其中 `$token` 必须来自当前目标 DLL。不要照抄其他版本 token。

`query.ps1` 实际按 Backend、Frontend 的顺序选择首个包含程序集的目录；省略 `-Assembly` 也优先 Backend，并不会同时搜索两端。`GameData.Shared`、Harmony 等两端共有 DLL 必须直接指定目录：

```powershell
$decompiler = Join-Path $PWD 'tools/TaiwuStudio.DecompilerWorker/bin/Release/net8.0/TaiwuStudio.DecompilerWorker.exe'
$raw = & $decompiler --command find-type --managed-dir $front --assembly GameData.Shared.dll --query Character --limit 10
$queryExit = $LASTEXITCODE
$result = ($raw -join "`n") | ConvertFrom-Json
if ($queryExit -ne 0 -or !$result.ok) { throw ($result.diagnostics | ConvertTo-Json -Depth 6) }
$result.payload.results
Get-FileHash -LiteralPath (Join-Path $front 'GameData.Shared.dll') -Algorithm SHA256
```

`decompile-member` 返回整个声明类型，`sourceSpan` 只是近似定位，需检查签名并截取相邻行，不把整个大类型输出到对话。零匹配不一定是工具故障。

## 正式双端构建

```powershell
& ./mods/TheScrollOfHomelander/Build-Deploy.ps1 -GameRoot $gameRoot
```

这会写入 `Plugins/Front`、`Plugins/Back`、`build-records/Frontend.json`、`Backend.json` 及 `release-manifest.json`，但不会部署。脚本遇到第一端失败即停止；此前旧清单或另一端旧产物可能仍在磁盘，不代表本次通过。两端记录必须来自本次源码和游戏程序集，`success` 为 true 且无 error；清单需在两端成功之后生成。

## 隔离诊断构建

需要保留正式 Plugins 或分别诊断失败时，可使用临时输出目录。它不是发布产物，不自动生成正式清单。

```powershell
$roslyn = Join-Path $PWD 'tools/TaiwuStudio.RoslynWorker/bin/Release/net8.0/TaiwuStudio.RoslynWorker.exe'
$modRoot = Join-Path $PWD 'mods/TheScrollOfHomelander'
$checkRoot = Join-Path ([IO.Path]::GetTempPath()) ('taiwu-mod-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkRoot | Out-Null
$failedSides = @()
foreach ($side in @(
    @{ Name = 'Frontend'; Libs = $front },
    @{ Name = 'Backend'; Libs = $back }
)) {
    $assembly = 'BetterTaiwuScroll' + $side.Name
    $output = Join-Path $checkRoot ($assembly + '.dll')
    $raw = & $roslyn build --project-root $modRoot --game-libs $side.Libs --assembly-name $assembly --output-plugin-path $output --source-dir ('Scripts/' + $side.Name) --source-dir Scripts/Shared --configuration Release
    $buildExit = $LASTEXITCODE
    $raw | Set-Content -LiteralPath (Join-Path $checkRoot ($side.Name + '.json')) -Encoding utf8
    $result = ($raw -join "`n") | ConvertFrom-Json
    if ($buildExit -ne 0 -or !$result.success -or @($result.diagnostics | Where-Object severity -eq 'error').Count -gt 0) { $failedSides += $side.Name }
}
Write-Output "诊断输出：$checkRoot"
if ($failedSides.Count -gt 0) { throw ('编译失败，禁止发布：' + ($failedSides -join ', ')) }
```

绝对输出路径避免相对路径被放入 Mod 的 Plugins。每端必须包括 Shared，不编译整个 Scripts 为单一程序集。报告错误与警告数量，不把 Windows 编译结果推断为游戏内验收。
