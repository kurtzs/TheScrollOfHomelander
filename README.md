# 太吾绘卷 Mod 工作区（Documents 副本）

- 复制时间：2026-09-16
- 来源：C:\Users\Administrator\Documents\GitHub\taiwu_studio
- 用途：独立携带 Mod 工程、技能说明、反编译程序与编译程序，可在本机或其他机器上直接继续开发。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| mods\TheScrollOfHomelander | 正式 Mod 工程：Scripts\Frontend、Scripts\Backend、Scripts\Shared、Assets、GradeBackgrounds、Config.lua、Settings.Lua，以及已编译的 Plugins\Front、Plugins\Back |
| tools/TaiwuStudio.RoslynWorker | 编译程序源码（C# 前端/后端编译，基于 Roslyn）；携带的 bin/Release 为 Windows 版本 |
| tools/TaiwuStudio.DecompilerWorker | 反编译程序源码（读取游戏程序集，查询类型/成员/源码）；携带的 bin/Release 为 Windows 版本 |
| tools/build-linux.sh、tools/run-worker.sh | Linux 隔离构建与启动入口；产物在 tools/.linux/ |
| skills | taiwu-linux-development、taiwu-mod-authoring、taiwu-decompiled-api、the-scroll-of-homelander-mod |
| opencode.json | 直接注册 skills/，不依赖旧 Windows 链接，不修改全局配置 |

`mods` 与 `tools` 的相对层级与源仓库保持一致。`Build-Deploy.ps1` 保留 Windows 工作流，不能直接视为 Linux 脚本。

## Linux 开发

当前工作区：`/run/media/shishanyue/Win11Pro X64/Users/Administrator/Documents/TheScrollOfHomelander-Workspace`。

游戏目录：`/home/shishanyue/.local/share/Steam/steamapps/common/The Scroll Of Taiwu`。

从工作区根目录运行：

```bash
bash tools/build-linux.sh
bash tools/run-worker.sh decompiler --help
```

工具继续以 `net8.0` 为目标，在本机 .NET SDK 10 下重新构建；启动器为工具进程使用 `Major` roll-forward，已在 .NET 10 运行时验证，不更改游戏环境。所有构建与还原产物均隔离，不覆盖 Windows 工具。

开发入口见 [taiwu-linux-development](skills/taiwu-linux-development/SKILL.md)，包含 API 查询、分端编译、Proton 日志/设置路径和发布门禁。现有三个专业 skills 保留并互相引用。重启 OpenCode 后可自动发现，`opencode debug skill` 可检查注册结果。

2026-09-29 验证：Steam app `838350`、build `25596993` 安装完成，两个工具构建和查询通过；当前 Mod 后端编译通过，前端有 85 个错误（包括 `UI_Make`、`UI_BuildingManage` 缺失），需要另行适配游戏 API。隔离编译不会更新正式 Plugins/清单；本次未部署、未启动游戏。Linux 完整发布/部署脚本尚未移植，不能把检查产物直接当作发布包。

## Windows 编译

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1
```

前端与后端分别编译，编译产物写入 `mods\TheScrollOfHomelander\Plugins`，同时写出 `build-records` 编译日志和 `release-manifest.json`（源码摘要、游戏程序集指纹、DLL/PDB 哈希）。

## Windows 部署到 Steam

```powershell
.\mods\TheScrollOfHomelander\Build-Deploy.ps1 -Deploy
```

会先完整备份现有 Steam Mod，再同步源码与 Plugins 下的 DLL/PDB，并逐个校验 SHA256。原有配置、存档与资产不会被覆盖；目标必须已存在 Config.lua，不能用此脚本直接引导 Linux 的全新安装。插件文件逐个原子替换，不代表整个发布原子完成。

游戏目录默认为 `C:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu`，如不同可用 `-GameRoot` 指定。

## 依赖与注意

- Windows 携带工具以 .NET 8 为目标；Linux 需要 .NET SDK 8 或更新版本及首次还原所需的 NuGet 访问，当前实际安装的是 SDK 10.0.111 / runtime 10.0.11。
- `C:\Users\Administrator\Documents\TheScrollOfHomelander` 是游戏保存 Mod 设置的数据目录（只有 JSON 配置），不是工程目录，请勿与源码混放。
- 修改 DLL 后必须完全退出并重启游戏才会生效。
- Linux/Proton 用户数据位置以实际前缀为准，不能把宿主 Documents、Windows 数据目录或游戏源码目录混为一谈。不启动游戏、不运行游戏内自动化验收。

## 版本控制

本工作区自 2026-09-16 起使用独立 Git 仓库（`master` 分支），不再跟随旧仓库
`C:\Users\Administrator\Documents\GitHub\taiwu_studio`。旧仓库的远端和历史保持不动。

- `tools\**\obj\` 与 `tools\**\bin\Debug\` 已忽略，`bin\Release` 随仓库携带，保证工具可用。
- Linux 工具及隔离 Mod 检查产物 `tools/.linux/` 已忽略，不向版本控制加入系统相关的还原缓存。
- Mod 构建产物 `Plugins`、`build-records`、`release-manifest.json` 由 Mod 自身 `.gitignore` 忽略，由 `Build-Deploy.ps1` 重新生成。
- 后续开发约定见根目录 `AGENTS.md`。
