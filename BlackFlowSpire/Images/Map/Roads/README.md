# 道路控制纹理

七张 `lava_line_*.png` 原样复制自 `artifacts/arknights-blackflow/map-background-lines/02_connections/images/`；溶解遮罩和 `path_find_full` 来自同目录的 `shared-assets/textures/`。文件仅去除提取时附带的路径ID后缀，没有修改像素。

`forest_map_road.gdshader` 按LavaLiquid2D提取参数合成RGB通道流体线，`ForestMapRoad.gd` 重播原展开动画的蓝通道Hermite曲线。可达／不可达使用不同流速、阈值、亮度和透明度。普通寻路使用独立贴图和材质，取消后淡出；尚未接入天气分支。

原Shader的编译程序、材质和动画源索引见根目录 `BLACKFLOW-MAP-BACKGROUND-LINES.md`。Godot版本为材质运算移植，运行时无需本机明日方舟资源。
