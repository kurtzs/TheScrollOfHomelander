---
name: taiwu-windows-development
description: 在 Windows 上开发、排查、编译和部署太祖绘卷 TheScrollOfHomelander Mod，处理 PowerShell 7、Steam 路径、.NET 8 workers、当前游戏程序集查询、双端构建与发布验证。用于本工作区的 Windows 开发任务；Linux 环境使用 taiwu-linux-development。
---

# 太祖绘卷 Windows 开发

这是 Windows 环境入口。先读工作区 `AGENTS.md`，以实际宿主环境判断命令，不把文档中历史的“当前在 Linux”当作运行环境。所有命令在 `C:/Users/Administrator/Documents/TheScrollOfHomelander-Workspace` 根目录用 PowerShell 7 执行。

## 按任务读取

| 任务 | 规则与参考 |
| --- | --- |
| 环境、游戏路径、构建和检查 workers | [environment-and-tools.md](references/environment-and-tools.md) |
| 查询当前 DLL、分端编译和诊断 | [api-and-build.md](references/api-and-build.md) |
| 发布、备份、配置和用户数据、日志 | [runtime-and-deploy.md](references/runtime-and-deploy.md) |
| 通用 Mod、Harmony、前后端协议 | [taiwu-mod-authoring](../taiwu-mod-authoring/SKILL.md) |
| 类型、重载、token 和源码证据 | [taiwu-decompiled-api](../taiwu-decompiled-api/SKILL.md) |
| 本 Mod 文件布局、开关和补丁分组 | [the-scroll-of-homelander-mod](../the-scroll-of-homelander-mod/SKILL.md) |

## 开发流程

1. 检查当前工作区及未提交修改。源码和工具只在本仓库维护；旧 `Documents/GitHub/taiwu_studio` 是历史存档，不向它的远端推送。以 `skills/` 为技能来源，不依赖 `.dsh/skills` 链接。
2. 确认实际 Steam 游戏路径、两端程序集和工具运行环境。游戏更新未完成或必要 DLL 缺失时，停止依赖游戏的查询、编译和部署，仍可处理独立文档或工具问题。
3. 修改 Mod 前用当前 `TaiwuStudio.DecompilerWorker` 反查接口，记录端别、DLL SHA256、完整类型、重载和 token；查不到时调查当前 API，不引用旧 DLL 掩盖变化。
4. 按通用和项目技能实现改动。前端负责 UI，后端负责权威游戏状态；Shared 同时编入两端；功能关闭时保留原版行为，保持 JSON 兼容。
5. 用仓库 `Build-Deploy.ps1` 编译两端并生成清单。隔离诊断可用直接 Roslyn 命令，但不能替代发布记录。任一端失败都禁止发布，也不能用已有 DLL 的存在判断本次成功。
6. 用户请求包含部署时，执行发布参考中的备份、目标范围和逐文件哈希检查。普通开发或技能维护不自动部署。构建成功仅证明编译通过。
7. 不启动游戏或游戏内自动化测试。部署后明确提示用户完全退出并重启游戏，再由用户进行游戏内验收；需要日志时读取用户此次运行产生的日志。

## 运行目标约束

Workers 的 `net8.0` 是工具运行时，不是 Mod 运行时。Mod 继续引用已安装游戏的程序集，保留 `UNITY_STANDALONE_WIN`，不要引入 .NET 10 运行库或把游戏 DLL 放进发布包。不把 Linux 的历史构建结果当作 Windows 当前兼容性结论。
