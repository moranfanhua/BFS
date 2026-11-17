# 素材来源与编辑入口

原作贴图、预制体、动画与反编译证据保存在本地 `artifacts/arknights-blackflow/`。下表的来源路径相对于该目录；提取与转换工具在本地 `tools/`，游戏运行只使用打包资源。

| 用途 | 原始资料 | Mod 中的入口 |
| --- | --- | --- |
| 区域背景与道路 | `map-background-lines/` 的 `backgrounds.json`、`01_backgrounds/`、`02_connections/`、`03_path_finding/` | [地图场景](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapScreen.tscn)、[道路场景](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapRoad.tscn)。道路纹理的 RGB 通道由 Shader 合成，`BlackFlowSpireMapRoad.gd` 播放原展开曲线。 |
| 当前位置与选中标记 | `map-node-markers/` 的原预制体与曲线；选中光圈使用原 Sprite，人体使用流体贴图与遮罩 | [当前位置](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapCurrentMarker.tscn)、[选中标记](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapSelection.tscn)、[动画数据](../BlackFlowSpire/Data/MapMarkers/) |
| 节点图标与详情 | `node-ui-catalog/` 的 `01_node_icons/`、`03_detail_ui/`、`08_map_parts/`；险路恶敌沿用尖塔 Boss 图标 | [详情场景](../BlackFlowSpire/Scenes/Map/BlackFlowSpireNodeDetails.tscn)、[节点视觉配置](../BlackFlowSpireCode/Map/Presentation/BlackFlowSpireNodeVisuals.cs) |
| 行动力和前往按钮 | `screenshot-assets/` 的 `rl06_zone_action_view_hierarchy.json`、`focus_view_hierarchy.json`；`movement-core-theme/native-dump/` 的 `RL06FocusConfirmButtonPlugin` 反编译记录 | [行动力](../BlackFlowSpire/Scenes/Map/BlackFlowSpireActionPoints.tscn)、[前往按钮](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMoveButton.tscn)、[旋涡特效](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMovementVfx.tscn) |
| 追猎 | `hunt-ui/` 的原层级、动画轨道及材质；[贴图来源清单](../BlackFlowSpire/Images/Map/Hunt/provenance.json) | [追猎场景](../BlackFlowSpire/Scenes/Map/Hunt/)、[入场轨道](../BlackFlowSpire/Data/Hunt/entry.json) |
| 失与得 | `event-choice-ui/` 的 `choice_view`、`choice_item`、`choice_scene_anim`；`sacrifice-ui/` 的 `sacrifice_view`、`sacrifice_item`、`sacrifice_item_icon` 和 `judge_dialog`；事件 CG 为 `pic_rogue_6_24` | [事件视图](../BlackFlowSpireCode/Ui/BlackFlowSpireSacrificeUi.cs)、[交换场景](../BlackFlowSpire/Scenes/Events/BlackFlowSpireSacrificePicker.tscn)、[选物和确认绑定](../BlackFlowSpireCode/Ui/BlackFlowSpireSacrificePicker.cs) |
| 诡意行商 | `shop-ui/`、`shop-extended-ui/`；零件按原 ID 取自 `images/05_entry/ui_roguelike_topic_item_h1_rogue_6_0/`（M 加工品、G 自然物、P 概念体） | [商店场景](../BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopScreen.tscn)、[零件图](../BlackFlowSpire/Images/Parts/) |
| 秘境行商 | `secret-merchant-ui/`；[贴图来源清单](../BlackFlowSpire/Images/Shop/Secret/source-catalog.json) | [商店场景](../BlackFlowSpire/Scenes/Shop/BlackFlowSpireSecretShopScreen.tscn)、[种子培育曲线](../BlackFlowSpire/Animations/Shop/cultivation.json) |
| 零件箱 | `scrap-box-ui/` 的底栏入口、仓库、球体、卡片、容量与详情分组 | [底栏](../BlackFlowSpire/Scenes/Ui/BlackFlowSpirePartsBar.tscn)、[仓库](../BlackFlowSpire/Scenes/Ui/BlackFlowSpireWarehouse.tscn) |

[字体目录](../BlackFlowSpire/Fonts/) 中的 `.tres` 随 PCK 打包，由场景和 [BlackFlowSpireFonts](../BlackFlowSpireCode/Ui/BlackFlowSpireFonts.cs) 共用：

| 资源 | 原字体与用途 |
| --- | --- |
| `Body` / `Title` | NotoSansHans-Medium 正文 / SourceHanSansCN-Bold 标题 |
| `Numbers` / `NumbersNormal` | Novecentowide-Medium 商品价格 / Novecentowide-Normal 仓库总估价 |
| `Bender` / `Estimate` | Bender 零件容量 / AEwide 底栏估价 |
| `ActionPoints` | NumFont 位图数字，字形指标从原 Unity fontsettings 转为 BMFont |

[音乐 bank 和 GUID 表](../BlackFlowSpire/Audio/) 由 FMOD Studio 2.03.06 生成，原始分段 FLAC 位于 `bgm/clips-flac/`。重建时用 `tools/BlackFlowSpire.Audio/build_music.py` 一并更新两者；曲目分配在 [BlackFlowSpireMusicDirector](../BlackFlowSpireCode/Audio/BlackFlowSpireMusicDirector.cs)，沿用游戏音乐通道。

重新运行 `PrepareHuntAssets.py`、`PrepareMarkerAssets.py` 或 `PrepareMovementAssets.py` 等本地转换工具可能覆盖生成的场景或材质。详细来源与迁移笔记见本地 `docs/local/`，各目录的旧素材说明归档在 `docs/local/resource-notes/`。

失与得的事件正文和选项对应原表 `scene_ro6_sacrifice*`、`choice_ro6_sacrifice*`。`RoguelikeChoiceState._SelectChoice` 和 `RoguelikeDungeonPage._JumpToSacrificeInChoiceScene` 对应事件页进入交换页；`RoguelikeSacrificeView.OnValueChanged`、`_UpdateSacrificeTypePanels` 和 `RoguelikeSacrificeConfirmView._RenderCost` 对应列表、选中详情及确认按钮的显隐；`RoguelikeSacrificeViewModel.SelectItem` 对应再次点击同一物品时取消选择。`RL06SacrificeModelParamBuilder.Build`、`RL06SacrificeSelectedView.RenderCustomInfo` 和 `RL06SacrificeConfirmBehaviour.OnConfirmClick` 对应可用零件、估价及卸下在用加工品前的确认。原版选物后由 `RoguelikeSacrificeState._OnConfirmSacrifice` 提交服务端。Mod 使用[本地交换规则](../BlackFlowSpireCode/Map/Exploration/BlackFlowSpireSacrificeRules.cs)决定产物和深层分支条件；「焚毁文明」可获得，原作 Boss 专属效果未移植。

`sacrifice_item/img_icon` 的列表图标容器为 100×100，`sacrifice_view/left_part/panel_icon/item_icon_container` 的详情容器为 160×160。`RL06SacrificeListItem.RenderItemIcon` 和 `RL06SacrificeSelectedView.RenderSelectedItemIcon` 分别让 `RL06ItemIconWithScrap.RenderScrap` 显示 `sacrifice_item_icon` 的普通零件图或大图。Godot 在 `BlackFlowSpireSacrificePicker.AddIcon` 按对应容器缩放原预制体的图层和剩余次数星标，详情场景沿用 `panel_sell_price` 的位置、字号和叠放顺序。
