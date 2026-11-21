# 素材来源与编辑入口

原作贴图、预制体、动画与反编译证据保存在本地 `artifacts/arknights-blackflow/`。下表的来源路径相对于该目录；提取与转换工具在本地 `tools/`，游戏运行只使用打包资源。

| 用途 | 原始资料 | Mod 中的入口 |
| --- | --- | --- |
| 战后二选一 | `battle-victory-ui/18_reward_relic_select_item`、`20_reward_scrap_select_item`：`RoguelikeRewardRelicSelectView.Render` / `RL06RewardScrapSelectView.Render` 使用固定入口图而非候选图。点选后，`RoguelikeRewardSelectView.OnRender` 和 `Adapter.RenderView` 按主题配置选择候选卡：藏品用 `11_reward_select_item` 的 `RenderCard`，零件（showType 33）用 `19_reward_scrap_item` 的 `RL06RewardScrapView.Render`，嵌入 `25_details_scrap`。 | [入口和候选数据绑定](../BlackFlowSpireCode/Ui/BlackFlowSpireBattleSettlementScreen.Choice.cs)；[选择页](../BlackFlowSpireCode/Ui/BlackFlowSpireBattleSettlementScreen.cs) 对应 `03_reward_select` 的居中横排、50 单位间距与 `05_reward_const_text` 分隔层。`RoguelikeRewardSelectState._InitIfNot` 启用 `28_navigation` 的 return（左上角退出图标），本项目返回时不领取奖励。 |
| 战利品领取与离开确认 | `battle-victory-ui/02_reward_list`、`09_reward_item_holder`、`10_reward_leave`。`RoguelikeRewardListLayout.InnerLayouter.RemoveViewTransition` 将领走的卡在 0.3 秒内上移 50 并淡出（正弦缓入缓出）；`GetMoveDelay/Duration/Ease` 让后续槽位延迟 0.2 秒，再线性移动 0.2 秒。`RoguelikeRewardLeaveView.OnClick/OnCancelClick/OnConfirmClick` 和 `RoguelikeRewardLeaveSwitch.GenerateTweenOfShow/Hide` 控制离开卡内的确认层（10 单位位移与 0.2 秒淡变）。 | [战利品界面](../BlackFlowSpireCode/Ui/BlackFlowSpireBattleSettlementScreen.cs) 的 `FadeClaimedCard`、`LayoutRewardRow`、`BuildLeaveCard` 与 `SetConfirmation`；指示三角、确认和取消都使用 `reward_leave` 的[原贴图](../BlackFlowSpire/Images/Ui/BattleSettlement/)。 |
| 区域背景与道路 | `map-background-lines/` 的 `backgrounds.json`、`01_backgrounds/`、`02_connections/`、`03_path_finding/` | [地图场景](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapScreen.tscn)、[道路场景](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapRoad.tscn)。道路纹理的 RGB 通道由 Shader 合成，`BlackFlowSpireMapRoad.gd` 播放原展开曲线。 |
| 当前位置与选中标记 | `map-node-markers/` 的原预制体与曲线；选中光圈使用原 Sprite，人体使用流体贴图与遮罩 | [当前位置](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapCurrentMarker.tscn)、[选中标记](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMapSelection.tscn)、[动画数据](../BlackFlowSpire/Data/MapMarkers/) |
| 节点图标与详情 | `node-ui-catalog/` 的 `01_node_icons/`、`03_detail_ui/`、`08_map_parts/`；险路恶敌沿用尖塔 Boss 图标 | [详情场景](../BlackFlowSpire/Scenes/Map/BlackFlowSpireNodeDetails.tscn)、[节点视觉配置](../BlackFlowSpireCode/Map/Presentation/BlackFlowSpireNodeVisuals.cs) |
| 行动力和前往按钮 | `screenshot-assets/` 的 `rl06_zone_action_view_hierarchy.json`、`focus_view_hierarchy.json`；`movement-core-theme/native-dump/` 的 `RL06FocusConfirmButtonPlugin` 反编译记录 | [行动力](../BlackFlowSpire/Scenes/Map/BlackFlowSpireActionPoints.tscn)、[前往按钮](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMoveButton.tscn)、[旋涡特效](../BlackFlowSpire/Scenes/Map/BlackFlowSpireMovementVfx.tscn) |
| 追猎 | `hunt-ui/` 的原层级、动画轨道及材质；[贴图来源清单](../BlackFlowSpire/Images/Map/Hunt/provenance.json) | [追猎场景](../BlackFlowSpire/Scenes/Map/Hunt/)、[入场轨道](../BlackFlowSpire/Data/Hunt/entry.json) |
| 失与得 | `event-choice-ui/` 的 `choice_view`、`choice_item`、`choice_scene_anim`；`sacrifice-ui/` 的 `sacrifice_view`、`sacrifice_item`、`sacrifice_item_icon` 和 `judge_dialog`；事件 CG 为 `pic_rogue_6_24` | [事件视图](../BlackFlowSpireCode/Ui/BlackFlowSpireSacrificeUi.cs)、[交换场景](../BlackFlowSpire/Scenes/Events/BlackFlowSpireSacrificePicker.tscn)、[选物和确认绑定](../BlackFlowSpireCode/Ui/BlackFlowSpireSacrificePicker.cs) |
| 先行一步 | `rogue_6.ab` 的 `expedition_view`、`expedition_char_card`、`expedition_selecting_char`、`panel_exped_return_dialog` 和 `entry_expedition`。派遣贴图来自 `rglktp_rogue_6.ab`，事件 CG 为 `pic_rogue_6_22` | [事件视图](../BlackFlowSpireCode/Ui/BlackFlowSpireExpeditionUi.cs)、[选牌场景](../BlackFlowSpire/Scenes/Events/BlackFlowSpireExpeditionPicker.tscn)、[归队场景](../BlackFlowSpire/Scenes/Events/BlackFlowSpireExpeditionReturn.tscn) |
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

先行一步的事件正文对应 `scene_ro6_scout_enter`、`scene_ro6_scout_1`、`scene_ro6_scout_2` 和 `choice_ro6_scout_1/2`。`RoguelikeDungeonPage._JumpToExpeditionInChoiceScene` 对应从事件进入选牌页。`RoguelikeExpeditionModel.UpdateSelectingCharId` 在再次点击已选对象时清空选择，`RoguelikeExpeditionView.OnValueChanged` 与 `RoguelikeExpeditionConfirmView.Render` 按选择刷新详情和按钮。`BlackFlowSpireExpeditionPicker` 将这些状态绑定到尖塔牌组，保留原预制体的五列、105×105 槽位和 8 像素间距。升级预览复用尖塔 `NUpgradePreview.Reload` 的卡牌克隆与升级流程，选牌预览和归队结算均按 `CardModel.IsUpgradable` 判断能否再升一级。

原版 `RoguelikeExpeditionState._OnConfirmExpedition` 提交派遣，`RL06ExpeditionReturnDialog.RenderSingle` 绑定归队文本并播放入场动画。Mod 在 [派遣存档](../BlackFlowSpireCode/Persistence/BlackFlowSpireExpeditionSave.cs) 暂存卡牌，在换层时归还并发放加工品。归队场景的主要图层和 23 条入场曲线来自 `panel_exped_return_dialog` 的 `entry_expedition`。奖励池采用原版物品表中七件标为 `NORMAL` 的加工品。

联机沿用 `expedition_view` 的选牌页和 `panel_exped_return_dialog` 的归队页，每台客户端绑定本人的牌组和归队结果。[事件交互](../BlackFlowSpireCode/Integration/BlackFlowSpireExpeditionRuntime.cs)按玩家确认派遣或留下，[队伍派遣记录](../BlackFlowSpireCode/Persistence/BlackFlowSpireExpeditionParty.cs)保存各自的卡牌，并在换层时统一结算奖励。

失与得的事件正文和选项对应原表 `scene_ro6_sacrifice*`、`choice_ro6_sacrifice*`。`RoguelikeChoiceState._SelectChoice` 和 `RoguelikeDungeonPage._JumpToSacrificeInChoiceScene` 对应事件页进入交换页；`RoguelikeSacrificeView.OnValueChanged`、`_UpdateSacrificeTypePanels` 和 `RoguelikeSacrificeConfirmView._RenderCost` 对应列表、选中详情及确认按钮的显隐；`RoguelikeSacrificeViewModel.SelectItem` 对应再次点击同一物品时取消选择。`RL06SacrificeModelParamBuilder.Build`、`RL06SacrificeSelectedView.RenderCustomInfo` 和 `RL06SacrificeConfirmBehaviour.OnConfirmClick` 对应可用零件、估价及卸下在用加工品前的确认。原版选物后由 `RoguelikeSacrificeState._OnConfirmSacrifice` 提交服务端。Mod 使用[本地交换规则](../BlackFlowSpireCode/Map/Exploration/BlackFlowSpireSacrificeRules.cs)决定产物和深层分支条件；「焚毁文明」可获得，原作 Boss 专属效果未移植。

`sacrifice_item/img_icon` 的列表图标容器为 100×100，`sacrifice_view/left_part/panel_icon/item_icon_container` 的详情容器为 160×160。`RL06SacrificeListItem.RenderItemIcon` 和 `RL06SacrificeSelectedView.RenderSelectedItemIcon` 分别让 `RL06ItemIconWithScrap.RenderScrap` 显示 `sacrifice_item_icon` 的普通零件图或大图。Godot 在 `BlackFlowSpireSacrificePicker.AddIcon` 按对应容器缩放原预制体的图层和剩余次数星标，详情场景沿用 `panel_sell_price` 的位置、字号和叠放顺序。


普通行商的藏品沿用原资源 `shop-ui/02_goods_card/prefab.json` 中的商品卡和售罄图层，图标来自 [藏品图集](../BlackFlowSpire/Images/Collectibles/)。原作 `RL06ScrapShopController._OnGoodsClicked` 调用 `RoguelikeShopDetailView.Render` 展示商品详情，购买入口为 `_OnBuyGoods`。Mod 复用[商品卡场景](../BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopGoodsTile.tscn)，由[商店界面](../BlackFlowSpireCode/Ui/BlackFlowSpireShopUi.cs)绑定藏品名、图标、说明、价格和售罄状态。[藏品货位](../BlackFlowSpireCode/Shop/BlackFlowSpireShopCollectibleEntry.cs)沿用尖塔 `MerchantEntry.OnTryPurchaseUnsynchronized` 检查库存和金币，购买后登记到队伍藏品栏。

联机购买的删牌和升级复用尖塔 `NDeckCardSelectScreen.Create` 和 `NDeckUpgradeSelectScreen.ShowScreen`。卡包和药水包沿用现有选择页，[个人领取界面](../BlackFlowSpireCode/Ui/BlackFlowSpireShopClaims.cs)为每名玩家展示自己的候选项并提交选择。
