# 黑流模式字体

字体随 PCK 打包，场景与 C# 共用此目录的 FontVariation 资源。无需在 Windows 安装字体。

| 资源 | 原字体 | 用途 |
|---|---|---|
| Body.tres | NotoSansHans-Medium | 正文、名称、按钮、行动力消耗 |
| Title.tres | SourceHanSansCN-Bold | 节点与追猎大标题、零件详情标题、模式入口标题 |
| Numbers.tres | Novecentowide-Medium | 商品价格、投资金额、数量 |
| NumbersNormal.tres | Novecentowide-Normal | 零件箱展开页总估价 |
| Bender.tres | JovannyLemonad-Bender | 零件容量 |
| Estimate.tres | AEwide | 底栏零件总估价 |
| ActionPoints.tres | NumFont | 地图剩余行动力，原始位图字体 |

数字与标题资源以 Body 为回退，支持缺失的标点和混合文字。正文和标题保留大小写字形，适用于当前中文、英文两份本地化。其他缺字仍由 Godot 的字体回退处理。

NumFont.png 为原始图集，NumFont.fnt 按原 Unity fontsettings 的字形矩形、UV 与 advance 转为 BMFont 格式；未重绘图集。原数字字形为 64×76，FNT 使用76像素作为基准字号，控件仍通过场景字号缩放。

场景默认字体由 Theme 的 default_font 设置；特殊控件用 Theme Overrides > Fonts 明确指定。C# 创建的正文通过 ForestTheme / ForestMapAssets 读取 ForestFonts.Body，不能在本地化刷新时把整棵场景统一替换成正文或尖塔字体。

原字体、组件路径和来源证据见项目根目录 BLACKFLOW-FONTS.md 与 artifacts/arknights-blackflow/fonts/。复制字体和更新场景的工具为 tools/BlackFlowSpire.AssetAudit/PrepareFontAssets.py；运行会先备份被修改的场景。
