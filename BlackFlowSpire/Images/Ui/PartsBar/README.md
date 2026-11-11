# 零件箱底部入口

以下10张PNG按字节复制自 `artifacts/arknights-blackflow/scrap-box-ui/01_bottom_entry/images`，来自原作 `panel_menu/panel_bottom_bar/container/menu_container/panel_scrap`，未重绘。完整资源与布局来源见工作区的 `BLACKFLOW-SCRAP-BOX-UI-ASSETS.md`。

| 模组文件 | 原始图片名 |
|---|---|
| eye.png | scrap_object_icon_eye |
| estimate.png | img_query_gold |
| glow-middle.png | fx_normal_1 |
| glow-soft.png | fx_normal_2 |
| glow-right.png | fx_normal_3 |
| glow-overload.png | fx_over |
| full.png | scrap_object_bg_max |
| overload.png | tag_over |
| collapse-mask.png | mask_scrap_btn_back |
| collapse-arrow.png | img_back_small |

使用原图尺寸和原入口350×84参考布局；白色光线为三层原图叠加与透明度动画，未迁移Unity专用材质、粒子和Shader。球体使用101×105的小图标，不使用展开仓库的大球体图层。

仓库展开时启用“收起”遮罩、箭头和本地化文字，同时调暗底栏数字与光效；普通入口默认保持原状态。
