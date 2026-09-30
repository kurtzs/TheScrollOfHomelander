# 发布、用户数据和验收

## 发布前检查

先读当前 `mods/TheScrollOfHomelander/Build-Deploy.ps1`，确认行为与本参考一致。用户已要求部署时可继续执行；只要求开发、检查或创建技能时不触发部署。

目标路径：

- Steam：`<gameRoot>/Mod/TheScrollOfHomelander`。
- 开发副本：默认 `%LOCALAPPDATA%/TaiwuStudio/ModDevelopment/TheScrollOfHomelander-quality`，可用 `-DevelopmentRoot` 指定。
- 备份：默认 Windows Documents 下 `TaiwuModBackups`，可用 `-BackupRoot` 指定。

必须保留所有将覆盖目标的完整备份，并核对备份相对路径及逐文件 SHA256。脚本只自动复制 Steam 目标作为备份，没有完整备份验证，也没有备份已存在的开发副本；部署前补齐这些检查。首次安装没有原目录则记录这一事实。备份必须放在目标目录之外。

## 当前脚本的实际边界

- `-Deploy` 重新编译两端；任一端失败不部署。成功后记录源码、四个核心游戏 DLL、Plugins 中 DLL/PDB 的哈希及源码摘要。
- Steam 目标必须已有 `Config.lua`，否则脚本失败。首次安装需要先制定完整包安装流程，不为绕过检查创建空 Config。
- 已存在的目标只更新 C# 源码、DLL/PDB 和清单；不会更新 Config、Settings、资产、图标或 Build-Deploy.ps1。新建开发副本才初始化脚本指定的配置和资产。
- DLL/PDB 逐文件通过临时文件替换；整个部署不是事务。中途失败可能留下混合版本，停止发布，依据完整备份恢复本次受影响目标并重新验证。
- 脚本检查 Plugins 与 Scripts 哈希，不证明所有包文件均已更新。清单中的 Config 等可能描述源码副本而非实际目标。
- Plugins 扫描会包含目录中的其他 DLL/PDB；发布前对照 Config 声明检查是否有遗留产物，不能默默发布旧文件。

若本次改了配置或资产，依据实际差异补充复制/合并步骤，保留用户设置，并记录最终目标各文件哈希。将源码清单、游戏程序集清单、构建产物和实际部署文件记录对应起来；未完成这些步骤不能报告整包发布完成。不要把旧 DLL 或单端成功当作替代品。

```powershell
# 完成目标确认、备份及包内容检查后，从工作区根目录执行。
& ./mods/TheScrollOfHomelander/Build-Deploy.ps1 -GameRoot $gameRoot -Deploy
```

核对两端成功记录、此次清单时间与摘要、每个目标 DLL/PDB 和实际复制文件的 SHA256；报告备份路径和所有部署目标。游戏 DLL 不属于 Mod 发布包。

## 用户数据与日志

`C:/Users/Administrator/Documents/TheScrollOfHomelander` 是游戏写入 JSON 的数据目录，不能存源码或被部署覆盖。Windows Documents 可能重定向；依据实际 `ModUserDataPaths` 实现及 `[Environment]::GetFolderPath('MyDocuments')` 确认当前用户数据位置，不混淆源码目录、Steam Mod、开发副本和 JSON 数据。

日志在实际用户的 `%USERPROFILE%/AppData/LocalLow` 下定位 `Player.log`，核对时间与本次游戏会话；不假定某个历史副本就是当前日志。重点检查 `[BetterTaiwuScroll]`、`Installed patch group:`、`Disabled patch group`、Harmony 目标失败及异常。

不启动游戏，不执行游戏内自动化验收。部署完成必须告诉用户：**完全退出并重启游戏，DLL 才会重新加载。** 给出与本次变更相关的正常、关闭开关、重复打开/刷新和存读档验收点。报告编译/静态 API/哈希检查与尚待用户完成的运行验证；如修改 Config，列出键和版本变动。
