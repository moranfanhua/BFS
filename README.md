# BlackFlowSpire（黑流尖塔）

《杀戮尖塔 2》Mod，将《明日方舟》黑流树海的探索玩法与界面移植到尖塔。消耗行动力探索五层地图，收集零件与藏品，在行动力耗尽后迎战追猎。

## 安装与游玩

1. 将完整的 RitsuLib（最低 `0.6.11`）放入游戏的 `mods/STS2-RitsuLib/` 并启用。本项目面向《杀戮尖塔 2》`0.113.0` API。
2. 将 Mod 包的 `BlackFlowSpire/` 放入游戏的 `mods/`，确认 `mods/BlackFlowSpire/` 内有 `BlackFlowSpire.dll`、`BlackFlowSpire.pck` 和 `mod_manifest.json`，然后启用黑流尖塔。
3. 单人游戏在模式选择页选「黑流尖塔」（有未完成对局时需先结束该局或切换存档）；联机由房主在选角界面打开「黑流尖塔（联机）」。

联机请使用相同版本。房主决定路线并结算行动力、迷雾、节点和探索奖励；队友接收本层状态，仍通过原版流程进房、战斗和选牌。每人的希望独立。如果在队伍移动或交易途中重连，请等该操作结束后再加入。

## 构建

1. 安装 .NET 9 SDK、Godot 4.5.1 .NET 和对应的 Windows 导出模板。
2. 将游戏 `data_sts2_windows_x86_64/` 下的 `sts2.dll`、`0Harmony.dll`、`GodotSharp.dll` 复制到 `dll/`，并在其中创建空的 `.gdignore`。将完整 RitsuLib 包放入 `dll/STS2-RitsuLib/`，其根目录应包含 `RitsuLib.References.props`。
3. 用 Godot 打开 `project.godot`，选择 `BlackFlowSpire PCK` 预设导出 PCK 到 `artifacts/mods/BlackFlowSpire/`。如果 DLL 和清单尚未生成，在项目根目录运行 `dotnet build BlackFlowSpire.csproj -c ExportRelease -r win-x64`。

包含开发脚本和工具的本地工作区也可运行 `.\build.ps1 -GodotPath <Godot 控制台程序路径>` 自动构建、检查和打包。

开发入口：[项目结构与约定](AGENTS.md) · [素材来源与编辑入口](docs/ASSETS.md)
