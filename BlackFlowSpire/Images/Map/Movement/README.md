# 行动力与前往 UI 素材

PNG 由用户本机明日方舟安装资源提取，来自 `artifacts/arknights-blackflow/screenshot-assets/`。本目录仅复制本次界面所需图片；安装目录不作修改。

- `bg_action_point.png`：行动力底板，原图包含小图标。
- `bg_action_walk.png`、`icon_walk.png`：当前移动方式为步行时的光圈和脚印。
- `img_move_scrap_bkg.png`：装载加工品的蓝紫底圈；实际物品图来自共享 `Images/Parts` 图库。
- `move_mode_icon_times.png`、`move_mode_icon_times_empty.png`：右上弧形次数星标，亮星读取剩余次数，空星显示已使用次数。
- `icon_change_back.png`、`icon_change.png`：加工品右下的原圆底和切换双箭头；点击仍打开零件箱。
- `text_walk_valid.png`、`text_can_not_reach.png`：两个互斥前往状态。
- `action_point_icon.png`、`action_point_reachable.png`：消耗提示，实际数值由 Label 绘制。
- `blue.png`、`green.png`、`final_green.png`、`grey.png`、`purple.png`、`red.png`、`yellow.png`：来自 `artifacts/arknights-blackflow/movement-core-theme/images/` 的原始七色圆心。步行按目标已显示的节点主题换图，隐藏战斗／事件使用灰色；加工品固定紫色，追猎沿用 `Images/Map/Hunt/` 的独立深红圆心。
- 其余特效纹理：原旋涡合成素材，外圈材质配色保持原有值。

原布局来自 `rl06_zone_action_view_hierarchy.json` 与 `focus_view_hierarchy.json`。转换保留两个圆形网格的顶点、UV、索引和六个材质的纹理、滚动、溶解、着色与混合参数。原 Shader 为 `Torappu/UI/UI-Fx-Common`；Godot 版本根据提取的属性与像素程序运算重写，并非直接运行 Unity 字节码。源游戏的业务动画脚本未提取，Mod 使用本地时间驱动。

复现资源转换（需保留 artifacts 下的提取结果和 UnityPy 依赖）：

```powershell
& 'C:\Users\MoXia\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' tools\BlackFlowSpire.AssetAudit\PrepareMovementAssets.py
& 'C:\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe' --headless --editor --import --path .
& 'C:\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe' --headless --path . --script res://tools/BlackFlowSpire.AssetAudit/ConvertMovementResources.gd
```

重新转换会恢复原材质默认值；手工调色前建议复制对应材质。运行 Mod 不需要 Python、UnityPy、明日方舟或提取工具。
