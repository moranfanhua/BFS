# 展开仓库原素材

此目录36张PNG按字节复制自工作区 `artifacts/arknights-blackflow/scrap-box-ui`，未重绘或编辑像素。来源与预制体目录见 `BLACKFLOW-SCRAP-BOX-UI-ASSETS.md`，迁移场景见 `WAREHOUSE-UI.md`。

| 模组图片 | 来源分组／原名 |
|---|---|
| background、total-value、estimate | 02_warehouse_panel：img_warehouse_bg、img_total_value_bg、img_query_gold |
| sort、sort-arrow、sort-line、scroll、back | 02_warehouse_panel：img_filter_bg、icon_anchor_up、img_filter_line、img_scroll_bar、img_back |
| card、price、equipped、loaded、empty-card | 04_item_cards：img_card_bg、img_value_bg、img_use_board、img_use、img_card_empty |
| dot-empty、dot-overflow | 05_capacity_dots：img_scrap_small_empty、img_scrap_small_overflow |
| dot-processed、dot-natural、dot-concept | 05_capacity_dots：img_scrap_small_normal_move、img_scrap_small_normal_value、img_scrap_small_normal_other |
| eye-roots、eye-bg、eye-specks、eye-ball、eye-iris、eye-frame、eye-blood、eye-branch | 03_warehouse_eye：root、bg_normal、bg_dot_01、eye_normal_bg、eye_normal_glow、eye_main、blood、banch |
| eye-bg-overflow、eye-ball-overflow、eye-iris-overflow、eye-frame-overflow | 03_warehouse_eye：bg_overflow、eye_overflow_bg、eye_overflow_glow、eye_main_overflow |
| equip、unequip、discard、discard-icon | 07_item_detail：btn_equip、btn_unequip、btn_drop、drop_icon |
| leaf-left、leaf-right | 07_item_detail：leaf_left、leaf_right |

大球体按原350×350布局分层，库存卡片使用425×151.5单元，估价胶囊保留原图九宫格边缘。类别底圈、次数星标和实际零件图标复用既有商店零件组件。未迁移Unity专用材质与粒子；球体光效采用图层透明度动画。
