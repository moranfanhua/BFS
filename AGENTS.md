# BlackFlowSpire

BlackFlowSpire（黑流尖塔）是《杀戮尖塔 2》的 Mod，将《明日方舟》黑流树海的探索玩法与界面移植到尖塔。项目使用 Godot 4.5.1 .NET 和 C#（.NET 9）。

本项目基于第三方 Mod 开发框架 **RitsuLib**。开发新功能时，可以复用框架提供的 API 来简化开发，例如局内存档、联机动作同步、本地化和音频。框架参考源码位于 `dllsrc/STS2-RitsuLib-main/`，本地依赖包位于 `dll/STS2-RitsuLib/`，由 `BlackFlowSpire.References.props` 统一引用。

## 项目结构

| 路径 | 用途 |
| --- | --- |
| `project.godot`、`BlackFlowSpire.csproj`、`BlackFlowSpire.sln` | Godot 工程、C# 主项目和解决方案。 |
| `BlackFlowSpireCode/` | Mod 的 C# 源码。`Entry.cs` 是入口；`Integration/` 对接尖塔原版；`Ui/` 负责 UI 和数据绑定；`Map/` 包含地图生成、探索规则、关卡、零件和藏品；`Combat/`、`Shop/`、`Persistence/`、`Audio/`、`Commands/` 分别负责战斗、商店、存档、音频和调试命令。 |
| `BlackFlowSpire/` | Mod 的 Godot 资源。`Scenes/` 存放 `.tscn` 场景，`Scripts/`、`Shaders/`、`Materials/`、`Animations/` 支撑界面表现，`Images/`、`Fonts/`、`Audio/` 存放素材，`Data/` 和 `localization/` 存放数据与中英文案。 |
| `artifacts/arknights-blackflow/` | 《明日方舟》黑流树海的本地解包参考，按功能归档原贴图、预制体、动画和已有的反编译记录。 |
| `dllsrc/sts2/`、`dllsrc/`、`dll/` | `dllsrc/sts2/` 是《杀戮尖塔 2》的反编译源码；`dllsrc/` 也保存框架参考源码，`dll/` 存放本地编译依赖。 |
| `tests/BlackFlowSpire.Tests/`、`tools/` | 本地测试，以及资源核对、地图生成与预览、音频和打包工具。 |
| `build.ps1`、`install-local.ps1` | 本地构建和安装入口。Mod 包输出到 `artifacts/mods/BlackFlowSpire/`。 |
| `README.md`、`docs/ASSETS.md`、`docs/local/` | 安装和构建说明、素材索引、本地开发与解包记录。 |

## 项目约定

1. **有原版 UI 可复用时，严格以解包的原 UI 为准，并核对实际反编译代码。** 先定位对应预制体或场景、贴图和动画，再找到原版对应的类与方法。布局和表现按原资源还原，显隐、状态切换、交互和数据绑定按反编译方法体的实际行为实现。在改动说明中注明对应的原资源、类与方法，以及本项目的实现位置。截图和静态素材不足以确认运行时行为。
2. **新增逻辑所需的 UI 无法复用原版时，调用 `dsh`（`mcp__dsh__ask_dsh`）协助开发 UI。** 提供相关的场景或代码、功能需求及交互状态，并将设计落实到本项目的场景和代码。若只有一部分无法复用，仅对该部分调用 `dsh`。
3. **测试和工具仅供本地使用。** 不将 `tests/`、`tools/` 中的文件提交或上传到远程仓库，也不随 Mod 发布。
