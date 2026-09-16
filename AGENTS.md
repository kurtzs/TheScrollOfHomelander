# 开发位置与工作约定

自 2026-09-16 起，太祖绘卷（TheScrollOfHomelander）Mod 的唯一开发目录是本仓库：

`C:\Users\Administrator\Documents\TheScrollOfHomelander-Workspace`

## 目录

- `mods\TheScrollOfHomelander`：Mod 正式源码、资产、Config/Settings 与已构建的 DLL/PDB。
- `tools\TaiwuStudio.RoslynWorker`：前端/后端编译程序（.NET 8），可执行文件在 `bin\Release\net8.0`。
- `tools\TaiwuStudio.DecompilerWorker`：游戏程序集反编译与接口查询程序，可执行文件在 `bin\Release\net8.0`。
- `skills`：只保留太吾 Mod 相关技能：`taiwu-mod-authoring`（Mod 开发/编译/部署）、`taiwu-decompiled-api`（游戏程序集接口查询）、`taiwu-studio-industrial`（Taiwu Studio 架构）。

旧目录 `C:\Users\Administrator\Documents\GitHub\taiwu_studio` 自本日期起只作历史存档；其 `mods\` 与 `tools\` 不再作为开发目标，也不要向它的 GitHub 远端推送本工作区的提交。

## 工作约定

- 源码修改、编译、部署都在本目录完成；部署目标仍是 Steam Mod 目录。
- 编译：`.\mods\TheScrollOfHomelander\Build-Deploy.ps1`；部署加 `-Deploy`（先完整备份 Steam Mod，再逐文件校验 SHA256）。
- 修改前先读懂当前游戏程序集接口：优先用 `tools\TaiwuStudio.DecompilerWorker` 反查类型与成员，不要凭旧源码猜测。
- 不启动游戏、不运行游戏内自动化测试；游戏内验收由用户完成。
- 部署后必须提示用户完全退出并重启游戏，DLL 才会重新加载。
- `C:\Users\Administrator\Documents\TheScrollOfHomelander` 是游戏写入 Mod 设置的数据目录（只有 JSON 配置），禁止把源码放进去或覆盖它。
