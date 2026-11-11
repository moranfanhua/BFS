# 地图位置和选中标记

12张PNG来自 `artifacts/arknights-blackflow/map-node-markers/`，原像素保持不变。`cursor_anchor`、`cursor_tip`、头部、阴影和星点来自当前位置预制体；两个 `img_focus_*` 来自选中节点预制体的实际Sprite引用，包含原光晕和小十字，不能使用同名的纯白控制遮罩替代。

`cursor_body_lava` 与 `cursor_body_mask` 是材质采样图。Godot使用移植后的LavaLiquid2D运算合成人体；其他Sprite按原预制体层级排布。原出现、人物循环、选中显隐曲线复制到 `Data/MapMarkers/`，通过Hermite切线采样播放。

来源与动画目录见根目录 `BLACKFLOW-MAP-NODE-MARKERS.md`。仅迁移核心定位／选中标注，未移植独立的气泡粒子与极坐标强调分支。素材不依赖用户的明日方舟安装目录。

节点名称底图 `node_name_bkg.png` 另从 `node-ui-catalog/08_map_parts/` 原样复制。`node_completed.svg` 为本Mod绘制的绿色完成图标，用于非空节点名称左侧，与位置／选中标记分开维护。
