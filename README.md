# 太吾绘卷 Mod 工作区（Documents 副本）

- 复制时间：2026-09-16
- 来源：C:\Users\Administrator\Documents\GitHub\taiwu_studio
- 用途：独立携带 Mod 工程、技能说明、反编译程序与编译程序，可在本机或其他机器上直接继续开发。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| mods\TheScrollOfHomelander | 正式 Mod 工程：Scripts\Frontend、Scripts\Backend、Scripts\Shared、Assets、GradeBackgrounds、Config.lua、Settings.Lua，以及已编译的 Plugins\Front、Plugins\Back |
| tools\TaiwuStudio.RoslynWorker | 编译程序（C# 前端/后端编译，基于 Roslyn），可执行文件在 bin\Release\net8.0\TaiwuStudio.RoslynWorker.exe |
| tools\TaiwuStudio.DecompilerWorker | 反编译程序（读取游戏程序集，查询类型/成员/源码），可执行文件在 bin\Release\net8.0\TaiwuStudio.DecompilerWorker.exe |
| skills | Codex 技能说明，其中 taiwu-mod-authoring、taiwu-decompiled-api、taiwu-studio-industrial 与本工程直接相关 |

`mods` 与 `tools` 的相对层级刻意与源仓库保持一致，因此工程内的 Build-Deploy.ps1 无需修改即可使用。

## 编译

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1
```

前端与后端分别编译，编译产物写入 `mods\TheScrollOfHomelander\Plugins`，同时写出 `build-records` 编译日志和 `release-manifest.json`（源码摘要、游戏程序集指纹、DLL/PDB 哈希）。

## 部署到 Steam

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1 -Deploy
```

会先完整备份现有 Steam Mod，再同步源码与声明的插件文件，并逐个校验 SHA256。原有配置、存档与资产不会被覆盖。

游戏目录默认为 `C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu`，如不同可用 `-GameRoot` 指定。

## 依赖与注意

- 需要 .NET 8 运行时（本机已安装 8.0.31）。
- `C:\Users\Administrator\Documents\TheScrollOfHomelander` 是游戏保存 Mod 设置的数据目录（只有 JSON 配置），不是工程目录，请勿与源码混放。
- 修改 DLL 后必须完全退出并重启游戏才会生效。

## 版本控制

本工作区自 2026-09-16 起使用独立 Git 仓库（`master` 分支），不再跟随旧仓库
`C:\Users\Administrator\Documents\GitHub\taiwu_studio`。旧仓库的远端和历史保持不动。

- `tools\**\obj\` 与 `tools\**\bin\Debug\` 已忽略，`bin\Release` 随仓库携带，保证工具可用。
- Mod 构建产物 `Plugins`、`build-records`、`release-manifest.json` 由 Mod 自身 `.gitignore` 忽略，由 `Build-Deploy.ps1` 重新生成。
- 后续开发约定见根目录 `AGENTS.md`。
