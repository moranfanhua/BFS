# 节点图标

PNG 原样复制自 `artifacts/arknights-blackflow/node-ui-catalog/01_node_icons/`；空白详情图来自该目录的 `03_detail_ui/icon_empty_node_focus_view.png`。来源及原始引用见根目录 `BLACKFLOW-NODE-UI-ASSETS.md` 与 catalog.json。

`ForestNodeVisuals.cs` 统一定义原始节点 ID、图标、详情专用图、主题和显示属性。`PORTAL`、`REST`、`EMPLOY` 仍为视觉占位；`BATTLE_SAVAGE` 已接入据点机制，专用战斗待补齐。没有复制明日方舟 Boss 专用图：险路恶敌继续使用尖塔原版 Boss。

`icon_savage_buoy_focus_view_node.png` 原样复制自同一目录的 `08_map_parts/`，用于流窜居民的地图叠加标记与详情图标，不改写底层节点图标。

同一提取目录的标题条、INFO 控件、提示条和主题 PNG 分别复制到 `../Details/`、`../Details/Hints/`、`../Details/Themes/`。提示条尚不显示；预留素材不代表已实现商品预览、传送等节点业务。没有修改图片像素，主题色来自 focus_view_color_data 的原始配置。布局与静态装饰由 Godot 场景实现，未移植这些节点的 Unity 粒子动画。
