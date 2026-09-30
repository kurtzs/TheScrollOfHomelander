# 环境与工具

使用 PowerShell 7（`pwsh`），不是 Windows PowerShell 5.1：发布脚本使用 `[IO.Path]::GetRelativePath`。路径含空格时用引号，执行路径用调用运算符 `&`。

```powershell
Set-Location -LiteralPath 'C:/Users/Administrator/Documents/TheScrollOfHomelander-Workspace'
$PSVersionTable.PSVersion
dotnet --list-sdks
dotnet --list-runtimes
git status --short
```

两个工具项目均为 `net8.0`，使用支持该目标的 SDK 和 .NET 8 运行时。工具缺失或源码改变时重新构建；不要为了绕过运行时缺失而修改 Mod 的引用或目标。

```powershell
$ErrorActionPreference = 'Stop'
foreach ($toolName in @('TaiwuStudio.RoslynWorker', 'TaiwuStudio.DecompilerWorker')) {
    dotnet build "tools/$toolName/$toolName.csproj" -c Release
    if ($LASTEXITCODE -ne 0) { throw "工具构建失败：$toolName" }
}
$decompiler = Join-Path $PWD 'tools/TaiwuStudio.DecompilerWorker/bin/Release/net8.0/TaiwuStudio.DecompilerWorker.exe'
& $decompiler --help
if ($LASTEXITCODE -ne 0) { throw 'Decompiler 启动失败' }
```

Windows 正式脚本使用上述 `bin/Release/net8.0/*.exe`。不要调用 Linux 的 `tools/.linux` 产物。帮助命令成功只证明工具可启动。

## 确认游戏安装

默认位置只是候选；如果不存在，从 Steam 库配置 `steamapps/libraryfolders.vdf` 和 app `838350` 的 `appmanifest_838350.acf` 确认实际库目录。不要通过启动 Steam 或游戏来验证。若 Steam 正在替换程序集，等安装稳定后重新查询和构建。

```powershell
$gameRoot = 'C:/Program Files (x86)/Steam/steamapps/common/The Scroll Of Taiwu'
$front = Join-Path $gameRoot 'The Scroll of Taiwu_Data/Managed'
$back = Join-Path $gameRoot 'Backend'
foreach ($dll in @('Assembly-CSharp', 'GameData.Shared', 'TaiwuModdingLib', '0Harmony', 'UnityEngine.CoreModule')) {
    $file = Join-Path $front ($dll + '.dll')
    if (!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -eq 0) { throw "缺少前端程序集：$file" }
}
foreach ($dll in @('GameData', 'GameData.Shared', 'TaiwuModdingLib', '0Harmony')) {
    $file = Join-Path $back ($dll + '.dll')
    if (!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -eq 0) { throw "缺少后端程序集：$file" }
}
```

这些命令设置的 `$gameRoot`、`$front`、`$back` 在 API 和构建参考中继续使用；新 PowerShell 会话需要重新设置。
