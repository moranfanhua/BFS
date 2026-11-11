# 追猎界面素材

来自用户本机已提取的 `artifacts/arknights-blackflow/hunt-ui/`。`provenance.json` 记录所用 PNG 的原始相对路径。未修改明日方舟安装目录。

`tools/BlackFlowSpire.AssetAudit/PrepareHuntAssets.py` 将原弹窗 RectTransform 层级转换为可编辑的 Godot 场景，并保留 84 条原始 Hermite 轨道及切线。轨道数据在 `BlackFlowSpire/Data/Hunt/entry.json`，播放脚本在 `BlackFlowSpire/Scripts/Map/ForestHuntArtwork.gd`。一条旧粒子路径重绑定到实际存在的 `group_bg/particle_ctrl_behind`。

源提取包没有导出材质的全部传递依赖，转换器按 CAB 索引补读 16 张纹理并缓存到 `hunt-ui/migration-textures/`，包括 `polar_red`、骨架／蛇形擦除遮罩与公共扭曲贴图。运行 Mod 不依赖这些提取工具或游戏安装资源。

普通步行使用原有紫色场景；追猎使用独立红色材质、圆芯、极坐标网格及猎犬图片。PNG 内的“出发前往”等文字按本次范围保留原图。弹窗、节点说明及其他界面文字通过 `nodes.json`、`exploration.json` 加载。

UI-Fx-Common 已移植参数着色／溶解／UV 扭曲运算，原关键帧驱动其颜色控制通道。模糊和环境光点用 Godot Shader 重建；未直接运行原 Unity 粒子与业务脚本，因此不声明与原游戏逐像素一致。

重新运行转换器会覆盖生成的 `ForestHuntArtwork.tscn` 和 `ForestHuntVfx.tscn`，修改这些场景之前请保留副本。可直接修改 `ForestHuntNotice.tscn`、`ForestHuntDetails.tscn` 的布局与字体。
